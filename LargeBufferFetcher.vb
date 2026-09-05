Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms

''' <summary>
''' Builds one large distributed.net buffer by running "dnetc -fetch" repeatedly and
''' merging every small buff-in file into a single target buffer.
'''
''' Per round:
'''   1. harvest anything already in buff-in into the target, then delete buff-in
'''   2. start the client with -fetch
'''   3. wait until buff-in has been quiet for QuietSeconds (FileSystemWatcher plus a
'''      length poll as a safety net - buffered appends do not always raise events) or
'''      until the client exits
'''   4. merge the records into the target, delete buff-in
'''
''' Records are copied byte-for-byte; nothing is re-scrambled or re-checksummed, because
''' a record's encoding does not depend on its position in the file. See BigBufferFile.
'''
''' Designed to be driven from a BackgroundWorker: set IsCancelRequested to a lambda that
''' returns worker.CancellationPending, and handle RoundCompleted to report progress.
''' </summary>
Public Class LargeBufferFetcher

#Region "Configuration"

    ''' <summary>Full path to dnetc.exe.</summary>
    Public Property ClientExePath As String = "dnetc.exe"

    ''' <summary>Directory the client runs in - the one containing buff-in.</summary>
    Public Property WorkingDirectory As String = "."

    ''' <summary>Name of the incoming buffer the client fills, e.g. "buff-in.r72".</summary>
    Public Property InBufferFileName As String = "buff-in.r72"

    ''' <summary>Full path of the merged buffer being built.</summary>
    Public Property BigBufferPath As String = "import_001.r72"

    ''' <summary>Extra arguments placed before -fetch, e.g. "-ini dnetc.ini".</summary>
    Public Property ExtraArguments As String = ""

    ''' <summary>How many times to invoke -fetch.</summary>
    Public Property Rounds As Integer = 10

    ''' <summary>Seconds without writes to buff-in before a fetch counts as finished.</summary>
    Public Property QuietSeconds As Double = 5.0

    ''' <summary>Give up on a single round after this long.</summary>
    Public Property RoundTimeout As TimeSpan = TimeSpan.FromMinutes(3)

    ''' <summary>Pause between rounds - be polite to the keyserver.</summary>
    Public Property DelayBetweenRounds As TimeSpan = TimeSpan.FromSeconds(2)

    ''' <summary>Abort after this many consecutive rounds that produced no new packets.</summary>
    Public Property MaxEmptyRounds As Integer = 3

    ''' <summary>Kill the client if it is still alive once buff-in has gone quiet.</summary>
    Public Property KillClientAfterQuiet As Boolean = True

    ''' <summary>Verify every record's checksum before accepting it.</summary>
    Public Property VerifyChecksums As Boolean = True

    ''' <summary>
    ''' Drop packets whose keyspace is already in the target. The de-duplication key is
    ''' built from RC5-72 fields, so leave this off for OGR.
    ''' </summary>
    Public Property SkipDuplicates As Boolean = True

    ''' <summary>Window style for the client process.</summary>
    Public Property ClientWindowStyle As ProcessWindowStyle = ProcessWindowStyle.Normal

    ''' <summary>Send a space after starting the client (dismisses its startup prompt).</summary>
    Public Property SendSpaceOnStart As Boolean = True

    ''' <summary>Send Alt+Space,N after starting the client to minimise it.</summary>
    Public Property MinimizeClient As Boolean = False

    ''' <summary>Hook this to a BackgroundWorker's CancellationPending.</summary>
    Public Property IsCancelRequested As Func(Of Boolean)

#End Region

#Region "Events"

    Public Event Log(sender As Object, message As String)
    Public Event RoundCompleted(sender As Object, e As FetchRoundEventArgs)

    Private Sub Say(format As String, ParamArray args As Object())
        RaiseEvent Log(Me, If(args Is Nothing OrElse args.Length = 0, format, String.Format(format, args)))
    End Sub

#End Region

    Private _stopRequested As Boolean
    Private ReadOnly _activityLock As New Object()
    Private _lastActivityUtc As DateTime

    ''' <summary>Ask the loop to stop as soon as the current round finishes.</summary>
    Public Sub RequestStop()
        _stopRequested = True
    End Sub

    Private ReadOnly Property Cancelled As Boolean
        Get
            If _stopRequested Then Return True
            If IsCancelRequested IsNot Nothing AndAlso IsCancelRequested().Invoke Then Return True
            Return False
        End Get
    End Property

    Public ReadOnly Property InBufferPath As String
        Get
            Return Path.Combine(WorkingDirectory, InBufferFileName)
        End Get
    End Property

#Region "Main loop"

    Public Function Run() As FetchRunResult
        _stopRequested = False

        Dim result As New FetchRunResult()
        Dim knownKeys As HashSet(Of String) = Nothing

        If Not File.Exists(BigBufferPath) Then
            BigBufferFile.CreateEmpty(BigBufferPath)
            Say("Created {0}", Path.GetFileName(BigBufferPath))
        End If

        If SkipDuplicates Then
            knownKeys = BigBufferFile.GetExistingKeys(BigBufferPath)
            If knownKeys.Count > 0 Then Say("Target already holds {0} packet(s).", knownKeys.Count)
        End If

        ' Fold in anything left over from a previous run before fetching.
        result.TotalRecords = Harvest(knownKeys, result)

        For round As Integer = 1 To Rounds
            If Cancelled Then
                result.StopReason = "Cancelled."
                Exit For
            End If

            result.Rounds = round
            Dim before As UInteger = result.TotalRecords

            Try
                RunOneFetch()
            Catch ex As Exception
                Say("Fetch failed: {0}", ex.Message)
                result.Errors += 1
            End Try

            result.TotalRecords = Harvest(knownKeys, result)
            Dim gained As Integer = CInt(result.TotalRecords) - CInt(before)

            If gained <= 0 Then
                result.ConsecutiveEmptyRounds += 1
                Say("Round {0} produced no new packets ({1} in a row).", round, result.ConsecutiveEmptyRounds)
            Else
                result.ConsecutiveEmptyRounds = 0
                Say("Round {0}: +{1} packet(s), {2} total.", round, gained, result.TotalRecords)
            End If

            RaiseEvent RoundCompleted(Me, New FetchRoundEventArgs(round, Rounds, gained,
                                                                  result.TotalRecords, result.StatsUnits))

            If result.ConsecutiveEmptyRounds >= MaxEmptyRounds Then
                result.StopReason = String.Format("{0} empty rounds in a row - keyserver or client problem.",
                                                  result.ConsecutiveEmptyRounds)
                Exit For
            End If

            If round < Rounds AndAlso DelayBetweenRounds > TimeSpan.Zero Then Sleep(DelayBetweenRounds)
        Next

        If String.IsNullOrEmpty(result.StopReason) Then result.StopReason = "Completed."

        Say("Finished: {0} packet(s) / {1} stats units in {2} ({3})",
            result.TotalRecords, result.StatsUnits, Path.GetFileName(BigBufferPath), result.StopReason)
        Return result
    End Function

#End Region

#Region "One fetch round"

    Private Sub RunOneFetch()
        Dim psi As New ProcessStartInfo(ClientExePath, (ExtraArguments & " -fetch").Trim()) With {
            .WorkingDirectory = WorkingDirectory,
            .UseShellExecute = True,
            .WindowStyle = ClientWindowStyle
        }

        Dim proc As Process = Nothing
        Try
            proc = Process.Start(psi)
            Thread.Sleep(250)   ' let the client get its window up
            If SendSpaceOnStart Then SendKeys.SendWait(" ")
            If MinimizeClient Then SendKeys.SendWait("% N")

            WaitForBufferQuiet(proc, InBufferPath)

            If proc IsNot Nothing AndAlso Not proc.HasExited Then
                If KillClientAfterQuiet Then
                    Say("Client still running after buffer went quiet - terminating.")
                    Try
                        proc.Kill()
                        proc.WaitForExit(5000)
                    Catch
                    End Try
                Else
                    proc.WaitForExit(10000)
                End If
            End If
        Finally
            If proc IsNot Nothing Then proc.Dispose()
        End Try

        Thread.Sleep(500)   ' let the OS flush and release the handle
    End Sub

    ''' <summary>
    ''' Blocks until buff-in has had no write activity for QuietSeconds, the client exits
    ''' and the file settles, or RoundTimeout elapses.
    ''' </summary>
    Private Sub WaitForBufferQuiet(proc As Process, watchPath As String)
        Dim dir = Path.GetDirectoryName(Path.GetFullPath(watchPath))
        Dim name = Path.GetFileName(watchPath)

        Touch()
        Dim startedUtc = DateTime.UtcNow
        Dim lastLength As Long = FileLengthOrMinusOne(watchPath)

        Using watcher As New FileSystemWatcher(dir, name)
            watcher.NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size Or
                                   NotifyFilters.FileName Or NotifyFilters.CreationTime
            AddHandler watcher.Changed, Sub(s, e) Touch()
            AddHandler watcher.Created, Sub(s, e) Touch()
            AddHandler watcher.Renamed, Sub(s, e) Touch()
            AddHandler watcher.Deleted, Sub(s, e) Touch()
            watcher.EnableRaisingEvents = True

            While True
                Thread.Sleep(250)

                ' Polling backstop: FileSystemWatcher regularly misses buffered appends.
                Dim len = FileLengthOrMinusOne(watchPath)
                If len <> lastLength Then
                    lastLength = len
                    Touch()
                End If

                Dim quiet As Double
                SyncLock _activityLock
                    quiet = (DateTime.UtcNow - _lastActivityUtc).TotalSeconds
                End SyncLock

                Dim exited As Boolean = (proc Is Nothing) OrElse proc.HasExited

                If exited AndAlso quiet >= 1.0 Then Return
                If quiet >= QuietSeconds Then Return

                If (DateTime.UtcNow - startedUtc) > RoundTimeout Then
                    Say("Round timed out after {0:0}s.", RoundTimeout.TotalSeconds)
                    Return
                End If

                If Cancelled Then Return
            End While
        End Using
    End Sub

    Private Sub Touch()
        SyncLock _activityLock
            _lastActivityUtc = DateTime.UtcNow
        End SyncLock
    End Sub

    Private Shared Function FileLengthOrMinusOne(path As String) As Long
        Try
            Dim fi As New FileInfo(path)
            If Not fi.Exists Then Return -1
            Return fi.Length
        Catch
            Return -1
        End Try
    End Function

#End Region

#Region "Harvest"

    ''' <summary>
    ''' Merges the current buff-in into the target and removes buff-in so the next fetch
    ''' starts from empty. Returns the new record count in the target.
    ''' </summary>
    Private Function Harvest(knownKeys As HashSet(Of String), result As FetchRunResult) As UInteger
        Dim src = InBufferPath
        If Not File.Exists(src) Then Return BigBufferFile.GetHeaderCount(BigBufferPath)

        Dim stats As MergeStats = Nothing
        Try
            BigBufferFile.MergeInto(BigBufferPath, src, VerifyChecksums, SkipDuplicates, knownKeys, stats)
        Catch ex As Exception
            Say("Merge failed: {0}", ex.Message)
            result.Errors += 1
            Return BigBufferFile.GetHeaderCount(BigBufferPath)
        End Try

        If stats.RecordsRejected > 0 Then
            Say("WARNING: {0} record(s) failed checksum and were dropped.", stats.RecordsRejected)
        End If

        result.StatsUnits += stats.StatsUnitsAdded
        result.RecordsRejected += stats.RecordsRejected
        result.RecordsDuplicate += stats.RecordsDuplicate

        If Not BigBufferFile.TryDelete(src) Then
            Say("WARNING: could not delete {0} - the next round may re-read it.", InBufferFileName)
        End If

        Return stats.TotalRecordsInTarget
    End Function

#End Region

    Private Sub Sleep(span As TimeSpan)
        Dim deadline = DateTime.UtcNow + span
        While DateTime.UtcNow < deadline
            If Cancelled Then Return
            Thread.Sleep(200)
        End While
    End Sub

End Class

''' <summary>Reported after every fetch round.</summary>
Public Class FetchRoundEventArgs
    Inherits EventArgs

    Public ReadOnly Property Round As Integer
    Public ReadOnly Property TotalRounds As Integer
    Public ReadOnly Property PacketsAdded As Integer
    Public ReadOnly Property TotalPackets As UInteger
    Public ReadOnly Property StatsUnits As ULong

    Public Sub New(currentRound As Integer, roundCount As Integer, added As Integer,
                   packetTotal As UInteger, units As ULong)
        _Round = currentRound
        _TotalRounds = roundCount
        _PacketsAdded = added
        _TotalPackets = packetTotal
        _StatsUnits = units
    End Sub
End Class

''' <summary>Outcome of a LargeBufferFetcher run.</summary>
Public Class FetchRunResult
    Public Property Rounds As Integer
    Public Property TotalRecords As UInteger
    Public Property StatsUnits As ULong
    Public Property RecordsDuplicate As Integer
    Public Property RecordsRejected As Integer
    Public Property Errors As Integer
    Public Property ConsecutiveEmptyRounds As Integer
    Public Property StopReason As String = ""
End Class
