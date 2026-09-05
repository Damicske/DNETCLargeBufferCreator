Imports System.ComponentModel
Imports System.IO
Imports System.Threading

Public Class FrmMain
    Private sFolder As String, sImportFileExt As String
    Private bLoaded As Boolean = False
    Private IsBusy As Boolean = False
    Private bDirFalse As Boolean = False
    Private bFetched As Boolean = False
    Private FetchStart As Date
    Private FetchCurrent As Date

    ' Snapshot of UI state taken on the UI thread before the worker starts, so the
    ' background worker never touches controls.
    Private sClientExe As String
    Private iRounds As Integer
    Private bAutoMin As Boolean
    Private bMergeMode As Boolean

    Private Sub BtnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click
        If bgwImport.IsBusy Then
            bgwImport.CancelAsync()
            btnImport.Enabled = False
            btnImport.Text = "Stopping..."
            TaskBarProgressWrapper.SetState(Handle, TaskbarState.Paused)
            Exit Sub
        End If

        numUpDown.Enabled = False
        btnBrowseClient.Enabled = False
        RbOgr.Enabled = False
        RbRc5.Enabled = False
        btnCreate.Enabled = False
        btnRefresh.Enabled = False
        txtDnetcFolder.Enabled = False
        IsBusy = True
        Try
            TaskBarProgressWrapper.SetState(Handle, TaskbarState.Normal)
            ProgressBar1.Maximum = lbFile.Items.Count
            CounterMax = ProgressBar1.Maximum
            ProgressBar1.Value = 0
            Counter = 0
            TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
            lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
            bgwImport.RunWorkerAsync()
            btnImport.Text = "Cancel import"
            IsBusy = True
        Catch ex As Exception
            MessageBox.Show(ex.ToString, "Importing Buffers", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            numUpDown.Enabled = True
            btnBrowseClient.Enabled = True
            RbOgr.Enabled = True
            RbRc5.Enabled = True
            btnRefresh.Enabled = True
            txtDnetcFolder.Enabled = True
        End Try
    End Sub

    Private Sub BtnBrowseClient_Click(sender As Object, e As EventArgs) Handles btnBrowseClient.Click
        With OFD
            .InitialDirectory = My.Settings.LastDir
            If OFD.ShowDialog() = DialogResult.OK Then
                txtDnetcFolder.Text = .FileName
                My.Settings.LastDir = txtDnetcFolder.Text
                RefreshList()
            End If
        End With
    End Sub

    Private Sub BtnCreate_Click(sender As Object, e As EventArgs) Handles btnCreate.Click
        If bgwFetch.IsBusy Then
            bgwFetch.CancelAsync()
            btnCreate.Enabled = False
            btnCreate.Text = "Stopping..."
            TaskBarProgressWrapper.SetState(Handle, TaskbarState.Paused)
            Exit Sub
        End If

        RefreshList()
        If lbFile.Items.Count > numUpDown.Value Then
            MessageBox.Show("Please first import the already created buffer files, before making new ones", "Large Buffer Creator", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Exit Sub
        End If

        If File.Exists(sFolder & "import_0" & sImportFileExt) Then
            MessageBox.Show("import_0 exists, import first because something went wrong with the last creation", "Large Buffer Creator", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Exit Sub
        End If

        My.Settings.ToFetch = numUpDown.Value
        numUpDown.Enabled = False
        btnBrowseClient.Enabled = False
        RbOgr.Enabled = False
        RbRc5.Enabled = False
        btnImport.Enabled = False
        btnRefresh.Enabled = False
        txtDnetcFolder.Enabled = False
        mnuOptionsApause.Enabled = False
        mnuOptionsMerge.Enabled = False
        sFolder = txtDnetcFolder.Text.Substring(0, InStrRev(txtDnetcFolder.Text, "\"))

        ' Snapshot everything the worker needs, on the UI thread.
        sClientExe = txtDnetcFolder.Text
        iRounds = CInt(numUpDown.Value)
        bAutoMin = My.Settings.AutoMin
        bMergeMode = mnuOptionsMerge.Checked

        bFetched = True
        Try
            If File.Exists(Path.Combine(sFolder, "buff-in" & sImportFileExt)) Then My.Computer.FileSystem.RenameFile(Path.Combine(sFolder, "buff-in" & sImportFileExt), "import_0" & sImportFileExt)
            TaskBarProgressWrapper.SetState(Handle, TaskbarState.Normal)
            ProgressBar1.Maximum = iRounds
            ProgressBar1.Value = 0
            TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
            lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
            lblStatus.Text = If(bMergeMode, "Merge mode - building one buffer file", "Classic mode - one file per fetch")
            FetchStart = Date.Now
            FetchCurrent = FetchStart
            If mnuOptionsApause.Checked Then
                Shell(txtDnetcFolder.Text & " -pause", AppWinStyle.NormalFocus)
                Thread.Sleep(50)
                SendKeys.SendWait(" ")
            End If
            bgwFetch.RunWorkerAsync()
            btnCreate.Text = "Cancel create"
            IsBusy = True
        Catch ex As Exception
            MessageBox.Show(ex.ToString, "Create Buffers", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            numUpDown.Enabled = True
            btnBrowseClient.Enabled = True
            RbOgr.Enabled = True
            RbRc5.Enabled = True
            btnImport.Enabled = True
            btnRefresh.Enabled = True
            txtDnetcFolder.Enabled = True
            mnuOptionsApause.Enabled = True
            mnuOptionsMerge.Enabled = True
        End Try
    End Sub

    Private Sub FrmMain_Load(sender As Object, e As EventArgs) Handles Me.Load
        Application.DoEvents()
        txtDnetcFolder.Text = My.Settings.LastDir
        mnuOptionsAimport.Checked = My.Settings.AutoImport
        MnuOptionsAutoMinClient.Checked = My.Settings.AutoMin
        mnuOptionsApause.Checked = My.Settings.AutoPause
        mnuOptionsMerge.Checked = My.Settings.MergeMode
        numUpDown.Value = My.Settings.ToFetch
        lblStatus.Text = ""
    End Sub

    Private Sub FrmMain_Activated(sender As Object, e As EventArgs) Handles Me.Activated
        bLoaded = True
        If Not IsBusy Then RefreshList()
    End Sub

    Private Sub FrmMain_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        My.Settings.Save()
    End Sub

    ''' <summary>
    ''' This will refresh the file list 
    ''' </summary>
    Private Sub RefreshList()
        lbFile.Items.Clear()
        My.Settings.LastDir = txtDnetcFolder.Text
        sFolder = txtDnetcFolder.Text.Substring(0, InStrRev(txtDnetcFolder.Text, "\"))
        If Not Directory.Exists(sFolder) Then
            If bDirFalse Then Exit Sub
            MsgBox("Directory doesn't exist any more, please change the directory to a valid one", MsgBoxStyle.Exclamation, "Directory check")
            bDirFalse = True
            Exit Sub
        Else
            bDirFalse = False
        End If
        For Each file As String In My.Computer.FileSystem.GetFiles(sFolder, FileIO.SearchOption.SearchTopLevelOnly, "import_*" & sImportFileExt)
            lbFile.Items.Add(Path.GetFileNameWithoutExtension(file))
        Next
        lbFile.Refresh()
        If lbFile.Items.Count = 0 Or bgwFetch.IsBusy Then
            btnImport.Enabled = False
        Else
            btnImport.Enabled = True
            lbFile.SelectedItem = 0
        End If
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        RefreshList()
    End Sub

    Private Sub RbOgr_CheckedChanged(sender As Object, e As EventArgs) Handles RbOgr.CheckedChanged
        sImportFileExt = ".og2"
        If bLoaded Then RefreshList()
    End Sub

    Private Sub RbRc5_CheckedChanged(sender As Object, e As EventArgs) Handles RbRc5.CheckedChanged
        sImportFileExt = ".r72"
        If bLoaded Then RefreshList()
    End Sub

    Private Sub BgwFetch_DoWork(sender As Object, e As DoWorkEventArgs) Handles bgwFetch.DoWork
        If bMergeMode Then
            DoMergeFetch(e)
            Exit Sub
        End If
        DoClassicFetch(e)
    End Sub

    ''' <summary>
    ''' Original behaviour: one -fetch per round, each result renamed to its own
    ''' import_NNN file, all of them imported afterwards one by one.
    ''' </summary>
    Private Sub DoClassicFetch(e As DoWorkEventArgs)
        Using p As New Process
            Dim psi As New ProcessStartInfo(sClientExe, " -fetch") With {
                .WindowStyle = ProcessWindowStyle.Normal
            }
            p.StartInfo = psi
            For i = 1 To iRounds
                If Not File.Exists(sFolder & "import_" & If(i < 100, "0", "") & If(i < 10, "0" & i.ToString, i.ToString) & sImportFileExt) Then
                    p.Start()
                    Thread.Sleep(250) 'wait until program is started
                    SendKeys.SendWait(" ")
                    If bAutoMin Then SendKeys.SendWait("% N")
                    p.WaitForExit()
                    If File.Exists(Path.Combine(sFolder, "buff-in" & sImportFileExt)) Then My.Computer.FileSystem.RenameFile(Path.Combine(sFolder, "buff-in" & sImportFileExt), "import_" & If(i < 100, "0", "") & If(i < 10, "0" & i.ToString, i.ToString) & sImportFileExt)
                End If
                bgwFetch.ReportProgress(CInt(i / iRounds * 100))
                If bgwFetch.CancellationPending Then Exit For
            Next
            psi = Nothing
        End Using
    End Sub

    ''' <summary>
    ''' Merge mode: fetch repeatedly, but instead of keeping one file per fetch, splice
    ''' the work records straight into a single large buffer file. Buffer records are
    ''' self-contained (own scramble seed in word 43, own checksum in word 42, no
    ''' whole-file CRC), so records can be copied between files byte for byte - only the
    ''' record count in the 32-byte header has to be corrected. See BigBufferFile.
    ''' The result is one import_NNN file, so the existing import step runs once instead
    ''' of once per fetch.
    ''' </summary>
    Private Sub DoMergeFetch(e As DoWorkEventArgs)
        Dim target As String = NextFreeImportPath()

        Dim fetcher As New LargeBufferFetcher With {
            .ClientExePath = sClientExe,
            .WorkingDirectory = sFolder,
            .InBufferFileName = "buff-in" & sImportFileExt,
            .BigBufferPath = target,
            .Rounds = iRounds,
            .QuietSeconds = 5.0,
            .VerifyChecksums = True,
            .SkipDuplicates = (sImportFileExt = ".r72"),
            .SendSpaceOnStart = True,
            .MinimizeClient = bAutoMin,
            .ClientWindowStyle = ProcessWindowStyle.Normal,
            .IsCancelRequested = Function() bgwFetch.CancellationPending
        }

        AddHandler fetcher.Log, Sub(s As Object, msg As String) Debug.WriteLine("[fetch] " & msg)
        AddHandler fetcher.RoundCompleted,
            Sub(s As Object, args As FetchRoundEventArgs)
                bgwFetch.ReportProgress(CInt(args.Round / iRounds * 100),
                                        String.Format("{0} packet(s), {1} stats units in {2}",
                                                      args.TotalPackets, args.StatsUnits,
                                                      Path.GetFileName(target)))
            End Sub

        e.Result = fetcher.Run()
    End Sub

    ''' <summary>First unused import_NNN name, so a merge never appends to an old file.</summary>
    Private Function NextFreeImportPath() As String
        For i = 1 To 999
            Dim candidate As String = Path.Combine(sFolder, "import_" & i.ToString("000") & sImportFileExt)
            If Not File.Exists(candidate) Then Return candidate
        Next
        Return Path.Combine(sFolder, "import_" & Date.Now.ToString("HHmmss") & sImportFileExt)
    End Function

    Private Sub BgwFetch_ProgressChanged(sender As Object, e As ProgressChangedEventArgs) Handles bgwFetch.ProgressChanged
        Try
            Dim int As Integer = CInt(e.ProgressPercentage / 100 * numUpDown.Value)
            If int > ProgressBar1.Maximum Then ProgressBar1.Maximum += 1
            ProgressBar1.Value = int
            LblEstTime.Text = Date.Now.AddSeconds((Date.Now - FetchCurrent).TotalSeconds * (ProgressBar1.Maximum - ProgressBar1.Value)).ToString("HH:mm:ss")
            FetchCurrent = Date.Now
            lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
            TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
            If e.UserState IsNot Nothing Then lblStatus.Text = e.UserState.ToString
        Catch ex As Exception
            Debug.WriteLine("BgwFetch_ProgressChanged: " & ex.ToString)
        End Try
    End Sub

    Private Sub RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles bgwFetch.RunWorkerCompleted
        If File.Exists(Path.Combine(sFolder, "import_0" & sImportFileExt)) Then My.Computer.FileSystem.RenameFile(Path.Combine(sFolder, "import_0" & sImportFileExt), "buff-in" & sImportFileExt)
        numUpDown.Enabled = True
        btnBrowseClient.Enabled = True
        RbOgr.Enabled = True
        RbRc5.Enabled = True
        btnCreate.Enabled = True
        btnCreate.Text = "Create buffers"
        btnImport.Enabled = True
        btnRefresh.Enabled = True
        LblEstTime.Text = "0:00:00"
        txtDnetcFolder.Enabled = True
        mnuOptionsMerge.Enabled = True
        IsBusy = False
        RefreshList()
        ProgressBar1.Value = 0
        TaskBarProgressWrapper.SetState(Handle, TaskbarState.NoProgress)
        If mnuOptionsApause.Checked Then
            Shell(txtDnetcFolder.Text & " -unpause", AppWinStyle.Hide)
            Thread.Sleep(50)
            SendKeys.SendWait(" ")
        End If
        mnuOptionsApause.Enabled = True

        ' Merge mode reports what it actually produced - worth showing even when
        ' auto-import takes over from here.
        Dim result As FetchRunResult = TryCast(e.Result, FetchRunResult)
        If e.Error IsNot Nothing Then
            lblStatus.Text = "Failed: " & e.Error.Message
            MessageBox.Show(e.Error.ToString, "Create Buffers", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
        ElseIf result IsNot Nothing Then
            lblStatus.Text = String.Format("{0} packet(s), {1} stats units - {2}",
                                           result.TotalRecords, result.StatsUnits, result.StopReason)
            If result.RecordsRejected > 0 Then
                MessageBox.Show(result.RecordsRejected & " record(s) failed their checksum and were dropped." & vbCrLf &
                                "The rest of the buffer is fine, but if this keeps happening turn merge mode off.",
                                "Create Buffers", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End If

        If mnuOptionsAimport.Checked Then
            BtnImport_Click(Me, e)
            Exit Sub
        End If

        MessageBox.Show("The fetching took " & Clock((Date.Now - FetchStart).TotalSeconds, False, False, False, False), "Run time", MessageBoxButtons.OK, MessageBoxIcon.Information)
        bFetched = False
    End Sub

    Public Shared Counter As Integer
    Public Shared CounterMax As Integer

    Private Sub BgwImport_DoWork(sender As Object, e As DoWorkEventArgs) Handles bgwImport.DoWork
        Using p As New Process
            Try
                Dim sFile As String
                For i = 0 To lbFile.Items.Count - 1
                    sFile = Path.Combine(sFolder, lbFile.Items.Item(i).ToString & sImportFileExt)
                    If File.Exists(sFile) Then
                        Dim psi As New ProcessStartInfo(txtDnetcFolder.Text, "-import " & sFile) With {.WindowStyle = ProcessWindowStyle.Maximized}
                        p.StartInfo = psi
                        p.Start()
                        Thread.Sleep(250)
                        'If CbDnetcMinimize.Checked Then
                        '    SendKeys.SendWait(" ")
                        '    SendKeys.SendWait("% N")
                        'Else
                        SendKeys.SendWait(" ")
                        'End If
                        p.WaitForExit()
                        File.Delete(sFile)
                    End If
                    Counter += 1
                    bgwImport.ReportProgress(CInt(Counter / CounterMax * 100))
                    If bgwImport.CancellationPending Then Exit For
                Next
                Debug.WriteLine("done")
            Catch ex As Exception
                Debug.WriteLine(ex.ToString)
            End Try
        End Using
    End Sub

    Private Sub BgwImport_ProgressChanged(sender As Object, e As ProgressChangedEventArgs) Handles bgwImport.ProgressChanged
        Try
            Dim int As Integer = CInt(e.ProgressPercentage / 100 * CounterMax)
            If int > ProgressBar1.Maximum Then ProgressBar1.Maximum += 1
            ProgressBar1.Value = int
            lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
            TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
        Catch ex As Exception
            Debug.WriteLine("BgwImport_ProgressChanged: " & ex.ToString)
        End Try
    End Sub

    Private Sub BgwImport_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles bgwImport.RunWorkerCompleted
        numUpDown.Enabled = True
        btnBrowseClient.Enabled = True
        RbOgr.Enabled = True
        RbRc5.Enabled = True
        btnCreate.Enabled = True
        btnImport.Text = "Import buffers"
        btnRefresh.Enabled = True
        txtDnetcFolder.Enabled = True
        IsBusy = False
        TaskBarProgressWrapper.SetState(Handle, TaskbarState.NoProgress)
        RefreshList()
        ProgressBar1.Value = 0
        If bFetched Then
            MessageBox.Show("The fetching and importing took " & Clock((Date.Now - FetchStart).TotalSeconds, False, False, False, False), "Run time", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
        bFetched = False
    End Sub

    Private Sub MnuOptionsAimport_Click(sender As Object, e As EventArgs) Handles mnuOptionsAimport.Click
        My.Settings.AutoImport = mnuOptionsAimport.Checked
    End Sub

    Private Sub MnuOptionsAutoMinClient_Click(sender As Object, e As EventArgs) Handles MnuOptionsAutoMinClient.Click
        My.Settings.AutoMin = MnuOptionsAutoMinClient.Checked
    End Sub

    Private Sub MnuOptionsApause_Click(sender As Object, e As EventArgs) Handles mnuOptionsApause.Click
        My.Settings.AutoPause = mnuOptionsApause.Checked
    End Sub

    Private Sub MnuOptionsMerge_Click(sender As Object, e As EventArgs) Handles mnuOptionsMerge.Click
        My.Settings.MergeMode = mnuOptionsMerge.Checked
    End Sub

    ''' <summary>
    ''' Sanity check on a buffer file: record count, stats units, and whether every
    ''' record's checksum still verifies. Handy after a merge.
    ''' </summary>
    Private Sub MnuToolsVerify_Click(sender As Object, e As EventArgs) Handles mnuToolsVerify.Click
        If lbFile.SelectedIndex < 0 Then
            MessageBox.Show("Select a buffer in the list first.", "Verify buffer", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Exit Sub
        End If

        Dim sFile As String = Path.Combine(sFolder, lbFile.SelectedItem.ToString & sImportFileExt)
        Try
            Dim buf = DnetcBufferFile.Load(sFile)
            Dim bad As Integer = 0
            For Each rec In buf.Records
                If Not rec.ChecksumValid Then bad += 1
            Next

            Dim expected As Long = 32 + CLng(buf.RecordCount) * 176
            Dim actual As Long = New FileInfo(sFile).Length

            MessageBox.Show(String.Format("{0}{1}{1}Header count: {2}{1}Records read: {3}{1}Stats units: {4}{1}Bad checksums: {5}{1}File size: {6} (expected {7})",
                                          Path.GetFileName(sFile), vbCrLf, buf.RecordCount, buf.Records.Count,
                                          buf.StatsUnitsCount, bad, actual, expected),
                            "Verify buffer", MessageBoxButtons.OK,
                            If(bad = 0 AndAlso actual = expected, MessageBoxIcon.Information, MessageBoxIcon.Warning))
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Verify buffer", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
        End Try
    End Sub

    Function Clock(iTime As Long, Optional GiveDays As Boolean = True, Optional GiveWeeks As Boolean = True, Optional MS As Boolean = True, Optional GiveMs As Boolean = True) As String
        Dim months As String = "", weeks As Integer = 0, days As Integer = 0, msec As Integer = 0
        '-set miliseconds to seconds
        If MS Then
            msec = iTime Mod 1000
            iTime /= 1000
        End If
        '-extract Time
        Dim uren As Integer = iTime \ 3600
        Dim minuten As Integer = Int((iTime - (uren * 3600)) \ 60)
        Dim seconden As Integer = iTime - (uren * 3600) - ((minuten * 60))
        '-days
        If GiveDays Then
            days = (uren \ 24)
            uren -= days * 24
        End If
        '-weeks
        If GiveWeeks Then
            weeks = (days \ 7)
            days -= weeks * 7
            '-months
            months = weeks \ (52 \ 12)
            If months = 0 Then
                months = ""
            Else
                weeks -= months * (52 \ 12)
                months &= " Months "
            End If
        End If

        Return months & If(GiveWeeks And weeks > 0, weeks & "W" & If(weeks = 1, "", "s") & " ", "") & If(GiveDays AndAlso days > 0, days & ".", "") & uren & ":" & If(minuten < 10, "0", "") & minuten &
        ":" & If(seconden < 10, "0", "") & seconden & If(GiveMs, "." & msec, "")
    End Function
End Class
