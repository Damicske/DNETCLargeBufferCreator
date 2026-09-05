Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading

''' <summary>
''' Record-level merge support for distributed.net buffer files (.r72 and friends).
'''
''' WHY THIS WORKS
''' --------------
''' A buffer file is a 32-byte header followed by N fixed-size (176 byte) records:
'''
'''   offset 0x00  u32 magic          (big-endian, 0x83B6341A)
'''   offset 0x04  u32 version        (big-endian, 0x48 = unlocked, 0x49 = locked)
'''   offset 0x08  u32 record count   (big-endian)
'''   offset 0x0C  20 bytes reserved
'''   offset 0x20  record[0] .. record[count-1]
'''
''' There is NO whole-file CRC. Each record is scrambled with a seed stored in its own
''' word 43, and checksummed into its own word 42 over words 0..41 (see
''' DnetcBufferRecord.Parse). Nothing in a record refers to its index or to any other
''' record, so records can be copied verbatim between files. Merging buffers therefore
''' reduces to: append record bytes, rewrite the count at offset 0x08. No re-encoding.
'''
''' NOTE: this class calls DnetcBufferRecord.Parse, which is Friend, so this file must
''' live in the SAME project/assembly as DnetcBufferReader.vb.
''' </summary>
Public NotInheritable Class BigBufferFile

    Public Const HeaderSize As Integer = &H20
    Public Const RecordSize As Integer = &HB0

    Private Const MagicExpected As UInteger = &H83B6341AUI
    Private Const VersionUnlocked As UInteger = &H48UI
    Private Const VersionLocked As UInteger = &H49UI

    Private Sub New()
        ' static class
    End Sub

#Region "Low level helpers"

    Public Shared Function ByteSwap32(v As UInteger) As UInteger
        Return ((v And &HFF000000UI) >> 24) Or ((v And &HFF0000UI) >> 8) Or
               ((v And &HFF00UI) << 8) Or ((v And &HFFUI) << 24)
    End Function

    Private Shared Function ReadFully(stream As Stream, buffer As Byte(), count As Integer) As Integer
        Dim total As Integer = 0
        While total < count
            Dim n = stream.Read(buffer, total, count - total)
            If n = 0 Then Exit While
            total += n
        End While
        Return total
    End Function

    ''' <summary>Big-endian u32 write into a byte array.</summary>
    Private Shared Sub PutBE32(target As Byte(), offset As Integer, value As UInteger)
        target(offset + 0) = CByte((value >> 24) And &HFFUI)
        target(offset + 1) = CByte((value >> 16) And &HFFUI)
        target(offset + 2) = CByte((value >> 8) And &HFFUI)
        target(offset + 3) = CByte(value And &HFFUI)
    End Sub

    ''' <summary>Big-endian u32 read from a byte array.</summary>
    Private Shared Function GetBE32(source As Byte(), offset As Integer) As UInteger
        Return (CUInt(source(offset + 0)) << 24) Or (CUInt(source(offset + 1)) << 16) Or
               (CUInt(source(offset + 2)) << 8) Or CUInt(source(offset + 3))
    End Function

#End Region

#Region "Reading"

    ''' <summary>
    ''' Reads every complete record from a buffer file as raw, untouched bytes.
    ''' Tolerates a header count that disagrees with the physical file size (which happens
    ''' if the client was killed mid-write) by using whichever is smaller, and silently
    ''' drops a trailing partial record.
    ''' </summary>
    ''' <param name="reserved">Receives the 20 reserved header bytes, so a merged file can
    ''' inherit them from a real client-produced buffer instead of inventing zeros.</param>
    Public Shared Function ReadRawRecords(sPath As String,
                                          <Runtime.InteropServices.Out> ByRef reserved As Byte(),
                                          <Runtime.InteropServices.Out> ByRef wasLocked As Boolean) As List(Of Byte())
        reserved = New Byte(19) {}
        wasLocked = False

        Dim records As New List(Of Byte())
        If Not File.Exists(sPath) Then Return records

        Using fs As New FileStream(sPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            If fs.Length < HeaderSize Then Return records

            Dim header(HeaderSize - 1) As Byte
            If ReadFully(fs, header, HeaderSize) <> HeaderSize Then Return records

            Dim magic As UInteger = GetBE32(header, 0)
            If magic <> MagicExpected Then
                Throw New InvalidDataException(
                    String.Format("{0} is not a distributed.net buffer file (magic 0x{1:X8}, expected 0x{2:X8}).",
                                  Path.GetFileName(sPath), magic, MagicExpected))
            End If

            wasLocked = (GetBE32(header, 4) = VersionLocked)
            Array.Copy(header, 12, reserved, 0, 20)

            Dim headerCount As UInteger = GetBE32(header, 8)
            Dim physicalCount As Long = (fs.Length - HeaderSize) \ RecordSize
            Dim toRead As Long = Math.Min(CLng(headerCount), physicalCount)

            For i As Long = 0 To toRead - 1
                Dim rec(RecordSize - 1) As Byte
                If ReadFully(fs, rec, RecordSize) <> RecordSize Then Exit For
                records.Add(rec)
            Next
        End Using

        Return records
    End Function

    ''' <summary>Convenience overload when the reserved bytes / lock state are not needed.</summary>
    Public Shared Function ReadRawRecords(path As String) As List(Of Byte())
        Dim reserved As Byte() = Nothing
        Dim locked As Boolean
        Return ReadRawRecords(path, reserved, locked)
    End Function

    ''' <summary>Record count straight from the header, without reading the records.</summary>
    Public Shared Function GetHeaderCount(path As String) As UInteger
        If Not File.Exists(path) Then Return 0UI
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            If fs.Length < HeaderSize Then Return 0UI
            Dim header(HeaderSize - 1) As Byte
            If ReadFully(fs, header, HeaderSize) <> HeaderSize Then Return 0UI
            If GetBE32(header, 0) <> MagicExpected Then Return 0UI
            Return GetBE32(header, 8)
        End Using
    End Function

#End Region

#Region "Record identity / validation"

    ''' <summary>
    ''' True if the record's stored checksum matches a freshly computed one. Uses the
    ''' existing reader so there is exactly one implementation of the scramble/checksum.
    ''' </summary>
    Public Shared Function IsRecordValid(recordBytes As Byte()) As Boolean
        Try
            Return DnetcBufferRecord.Parse(recordBytes).ChecksumValid
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Stable identity for a work packet: start key (words 0-2) plus the block size
    ''' (iterations, words 11-12) plus the contest id (word 37). Two records with the same
    ''' key describe the same keyspace and only one of them is worth keeping.
    ''' </summary>
    Public Shared Function GetRecordKey(recordBytes As Byte()) As String
        Dim rec = DnetcBufferRecord.Parse(recordBytes)
        Return String.Format("{0:X8}-{1:X8}-{2:X8}-{3:X8}-{4:X8}-C{5}",
                             rec.HostWords(0), rec.HostWords(1), rec.HostWords(2),
                             rec.HostWords(11), rec.HostWords(12), rec.HostWords(37))
    End Function

    ''' <summary>Stats units (2^28 key blocks) a record is worth, for progress reporting.</summary>
    Public Shared Function GetStatsUnits(recordBytes As Byte()) As UInteger
        Try
            Return DnetcBufferRecord.Parse(recordBytes).StatsUnitsInPacket
        Catch
            Return 0UI
        End Try
    End Function

    ''' <summary>Keys of every record already sitting in a buffer file, for de-duplication.</summary>
    Public Shared Function GetExistingKeys(path As String) As HashSet(Of String)
        Dim keys As New HashSet(Of String)(StringComparer.Ordinal)
        For Each rec In ReadRawRecords(path)
            Try
                keys.Add(GetRecordKey(rec))
            Catch
                ' unparseable record - ignore, it just won't take part in de-duplication
            End Try
        Next
        Return keys
    End Function

#End Region

#Region "Writing / merging"

    ''' <summary>Creates (or overwrites) a buffer file containing zero records.</summary>
    Public Shared Sub CreateEmpty(sPath As String, Optional reserved As Byte() = Nothing)
        Dim header(HeaderSize - 1) As Byte
        PutBE32(header, 0, MagicExpected)
        PutBE32(header, 4, VersionUnlocked)
        PutBE32(header, 8, 0UI)
        If reserved IsNot Nothing Then
            Array.Copy(reserved, 0, header, 12, Math.Min(20, reserved.Length))
        End If

        Dim dir = Path.GetDirectoryName(Path.GetFullPath(sPath))
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

        Using fs As New FileStream(sPath, FileMode.Create, FileAccess.Write, FileShare.None)
            fs.Write(header, 0, HeaderSize)
            fs.Flush(True)
        End Using
    End Sub

    ''' <summary>
    ''' Appends raw records to a buffer file (creating it if needed) and rewrites the
    ''' header count. Records are written byte-for-byte; nothing is re-scrambled or
    ''' re-checksummed, because a record's encoding does not depend on where it lives.
    ''' </summary>
    ''' <returns>The new total record count in the target file.</returns>
    Public Shared Function AppendRecords(path As String,
                                         records As IList(Of Byte()),
                                         Optional reservedTemplate As Byte() = Nothing) As UInteger
        If records Is Nothing Then records = New List(Of Byte())()

        If Not File.Exists(path) OrElse New FileInfo(path).Length < HeaderSize Then
            CreateEmpty(path, reservedTemplate)
        End If

        Using fs As New FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Dim header(HeaderSize - 1) As Byte
            If ReadFully(fs, header, HeaderSize) <> HeaderSize Then
                Throw New InvalidDataException("Target buffer file header is truncated: " & path)
            End If
            If GetBE32(header, 0) <> MagicExpected Then
                Throw New InvalidDataException("Target file is not a distributed.net buffer file: " & path)
            End If

            ' Trim any trailing partial record left behind by an interrupted write, so we
            ' never append behind garbage.
            Dim wholeRecords As Long = (fs.Length - HeaderSize) \ RecordSize
            Dim wantedLength As Long = HeaderSize + wholeRecords * RecordSize
            If fs.Length <> wantedLength Then fs.SetLength(wantedLength)

            fs.Seek(0, SeekOrigin.End)
            For Each rec In records
                If rec Is Nothing OrElse rec.Length <> RecordSize Then
                    Throw New ArgumentException("Every record must be exactly " & RecordSize & " bytes.")
                End If
                fs.Write(rec, 0, RecordSize)
            Next

            Dim newCount As UInteger = CUInt(wholeRecords + records.Count)

            ' Always mark the merged file unlocked - we are not the client.
            Dim countBytes(3) As Byte
            PutBE32(countBytes, 0, newCount)
            fs.Seek(8, SeekOrigin.Begin)
            fs.Write(countBytes, 0, 4)

            Dim verBytes(3) As Byte
            PutBE32(verBytes, 0, VersionUnlocked)
            fs.Seek(4, SeekOrigin.Begin)
            fs.Write(verBytes, 0, 4)

            fs.Flush(True)
            Return newCount
        End Using
    End Function

    ''' <summary>
    ''' Merges one buffer file into another. Optionally verifies each record's checksum and
    ''' skips packets already present in the target (or in <paramref name="knownKeys"/>,
    ''' which is updated in place so repeated calls stay cheap).
    ''' </summary>
    Public Shared Function MergeInto(targetPath As String,
                                     sourcePath As String,
                                     Optional verifyChecksums As Boolean = True,
                                     Optional skipDuplicates As Boolean = True,
                                     Optional knownKeys As HashSet(Of String) = Nothing,
                                     Optional ByRef stats As MergeStats = Nothing) As Integer
        If stats Is Nothing Then stats = New MergeStats()

        Dim reserved As Byte() = Nothing
        Dim locked As Boolean
        Dim source = ReadRawRecords(sourcePath, reserved, locked)
        stats.RecordsSeen += source.Count

        If skipDuplicates AndAlso knownKeys Is Nothing Then
            knownKeys = GetExistingKeys(targetPath)
        End If

        Dim accepted As New List(Of Byte())
        For Each rec In source
            If verifyChecksums AndAlso Not IsRecordValid(rec) Then
                stats.RecordsRejected += 1
                Continue For
            End If
            If skipDuplicates Then
                Dim key As String
                Try
                    key = GetRecordKey(rec)
                Catch
                    stats.RecordsRejected += 1
                    Continue For
                End Try
                If knownKeys.Contains(key) Then
                    stats.RecordsDuplicate += 1
                    Continue For
                End If
                knownKeys.Add(key)
            End If
            accepted.Add(rec)
            stats.StatsUnitsAdded += GetStatsUnits(rec)
        Next

        If accepted.Count > 0 Then
            stats.TotalRecordsInTarget = AppendRecords(targetPath, accepted, reserved)
        Else
            If Not File.Exists(targetPath) Then CreateEmpty(targetPath, reserved)
            stats.TotalRecordsInTarget = GetHeaderCount(targetPath)
        End If

        stats.RecordsAdded += accepted.Count
        Return accepted.Count
    End Function

    ''' <summary>
    ''' Deletes a file, retrying briefly - the client can still be holding the handle for a
    ''' moment after it exits.
    ''' </summary>
    Public Shared Function TryDelete(path As String, Optional attempts As Integer = 10,
                                     Optional delayMs As Integer = 250) As Boolean
        For i As Integer = 1 To attempts
            Try
                If Not File.Exists(path) Then Return True
                File.Delete(path)
                Return True
            Catch ex As IOException
                Thread.Sleep(delayMs)
            Catch ex As UnauthorizedAccessException
                Thread.Sleep(delayMs)
            End Try
        Next
        Return Not File.Exists(path)
    End Function

#End Region

End Class

''' <summary>Counters filled in by BigBufferFile.MergeInto.</summary>
Public Class MergeStats
    Public Property RecordsSeen As Integer
    Public Property RecordsAdded As Integer
    Public Property RecordsDuplicate As Integer
    Public Property RecordsRejected As Integer
    Public Property StatsUnitsAdded As ULong
    Public Property TotalRecordsInTarget As UInteger

    Public Overrides Function ToString() As String
        Return String.Format("seen={0} added={1} dup={2} bad={3} total={4}",
                             RecordsSeen, RecordsAdded, RecordsDuplicate, RecordsRejected,
                             TotalRecordsInTarget)
    End Function
End Class
