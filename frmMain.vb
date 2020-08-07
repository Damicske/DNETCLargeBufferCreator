Imports System.ComponentModel
Imports System.IO
Imports System.Threading

Public Class frmMain
    Private sFolder As String, sImportFileExt As String
    Private bLoaded As Boolean = False
    Private IsBusy As Boolean = False
    Private bDirFalse As Boolean = False
    Private bFetched As Boolean = False
    Private FetchStart As Date
    Private FetchCurrent As Date

    Private Sub BtnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click
        If bgwImport.IsBusy Then
            bgwImport.CancelAsync()
            btnImport.Enabled = False
            btnImport.Text = "Stopping..."
            TaskBarProgressWrapper.SetState(Handle, TaskbarState.Paused)
        Else
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
                MsgBox(ex.ToString, MsgBoxStyle.Exclamation, "Importing Buffers")
                numUpDown.Enabled = True
                btnBrowseClient.Enabled = True
                RbOgr.Enabled = True
                RbRc5.Enabled = True
                btnRefresh.Enabled = True
                txtDnetcFolder.Enabled = True
            End Try
        End If
    End Sub

    Private Sub BtnBrowseClient_Click(sender As Object, e As EventArgs) Handles btnBrowseClient.Click
        With OFD
            .InitialDirectory = My.Settings.LastDir
            If (OFD.ShowDialog() = DialogResult.OK) Then
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
        Else
            RefreshList()
            If lbFile.Items.Count > numUpDown.Value Then
                MsgBox("Please first import the already created buffer files, before making new ones", MsgBoxStyle.Exclamation)
                Exit Sub
            End If

            If File.Exists(sFolder & "import_0" & sImportFileExt) Then
                MsgBox("import_0 exists, import first because something went wrong with the last creation", MsgBoxStyle.Exclamation)
                Exit Sub
            End If

            numUpDown.Enabled = False
            btnBrowseClient.Enabled = False
            RbOgr.Enabled = False
            RbRc5.Enabled = False
            btnImport.Enabled = False
            btnRefresh.Enabled = False
            txtDnetcFolder.Enabled = False
            sFolder = txtDnetcFolder.Text.Substring(0, InStrRev(txtDnetcFolder.Text, "\"))
            bFetched = True
            Try
                If File.Exists(sFolder & "buff-in" & sImportFileExt) Then My.Computer.FileSystem.RenameFile(sFolder & "buff-in" & sImportFileExt, "import_0" & sImportFileExt)
                TaskBarProgressWrapper.SetState(Handle, TaskbarState.Normal)
                ProgressBar1.Maximum = CInt(numUpDown.Value)
                ProgressBar1.Value = 0
                TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
                lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
                FetchStart = Date.Now
                FetchCurrent = FetchStart
                bgwFetch.RunWorkerAsync()
                btnCreate.Text = "Cancel create"
                IsBusy = True
            Catch ex As Exception
                MsgBox(ex.ToString, MsgBoxStyle.Exclamation, "Create Buffers")
                numUpDown.Enabled = True
                btnBrowseClient.Enabled = True
                RbOgr.Enabled = True
                RbRc5.Enabled = True
                btnImport.Enabled = True
                btnRefresh.Enabled = True
                txtDnetcFolder.Enabled = True
            End Try
        End If
    End Sub

    Private Sub FrmMain_Load(sender As Object, e As EventArgs) Handles Me.Load
        Application.DoEvents()
        txtDnetcFolder.Text = My.Settings.LastDir
        mnuOptionsAimport.Checked = My.Settings.AutoImport
        MnuOptionsAutoMinClient.Checked = My.Settings.AutoMin
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
        Using p As New Process
            For i = 1 To CShort(numUpDown.Value)
                If Not File.Exists(sFolder & "import_" & If(i < 100, "0", "") & If(i < 10, "0" & i.ToString, i.ToString) & sImportFileExt) Then
                    Dim psi As New ProcessStartInfo(txtDnetcFolder.Text, " -fetch")
                    p.StartInfo = psi
                    p.Start()
                    Thread.Sleep(500) 'wait until program is started

                    If My.Settings.AutoMin Then
                        SendKeys.SendWait(" ")
                        SendKeys.SendWait("% N")
                    Else
                        SendKeys.SendWait(" ")
                    End If
                    p.WaitForExit()
                    If File.Exists(sFolder & "buff-in" & sImportFileExt) Then My.Computer.FileSystem.RenameFile(sFolder & "buff-in" & sImportFileExt, "import_" & If(i < 100, "0", "") & If(i < 10, "0" & i.ToString, i.ToString) & sImportFileExt)
                    psi = Nothing
                End If
                bgwFetch.ReportProgress(CInt(i / numUpDown.Value * 100))
                If bgwFetch.CancellationPending Then Exit For
            Next
        End Using
    End Sub

    Private Sub BgwFetch_ProgressChanged(sender As Object, e As ProgressChangedEventArgs) Handles bgwFetch.ProgressChanged
        Try
            Dim int As Integer = CInt(e.ProgressPercentage / 100 * numUpDown.Value)
            If int > ProgressBar1.Maximum Then ProgressBar1.Maximum += 1
            ProgressBar1.Value = int
            LblEstTime.Text = Date.Now.AddSeconds((Date.Now - FetchCurrent).TotalSeconds * (ProgressBar1.Maximum - ProgressBar1.Value)).ToString("HH:mm:ss")
            FetchCurrent = Date.Now
            lblBuffers.Text = ProgressBar1.Value & "/" & ProgressBar1.Maximum
            TaskBarProgressWrapper.SetValue(Handle, ProgressBar1.Value, ProgressBar1.Maximum)
        Catch ex As Exception
            Debug.WriteLine("BgwFetch_ProgressChanged: " & ex.ToString)
        End Try
    End Sub

    Private Sub RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles bgwFetch.RunWorkerCompleted
        If File.Exists(sFolder & "import_0" & sImportFileExt) Then My.Computer.FileSystem.RenameFile(sFolder & "import_0" & sImportFileExt, "buff-in" & sImportFileExt)
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
        IsBusy = False
        RefreshList()
        ProgressBar1.Value = 0
        TaskBarProgressWrapper.SetState(Handle, TaskbarState.NoProgress)
        If mnuOptionsAimport.Checked Then
            BtnImport_Click(Me, e)
        Else
            MessageBox.Show("The fetching took " & Clock((Date.Now - FetchStart).TotalSeconds, False, False, False, False), "Run time", MessageBoxButtons.OK, MessageBoxIcon.Information)
            bFetched = False
        End If

    End Sub

    Public Shared Counter As Integer
    Public Shared CounterMax As Integer

    Private Sub BgwImport_DoWork(sender As Object, e As DoWorkEventArgs) Handles bgwImport.DoWork
        Dim sFile As String
        Using p As New Process
            Try
                For i = 0 To lbFile.Items.Count - 1
                    sFile = Path.Combine(sFolder, lbFile.Items.Item(i).ToString & sImportFileExt)
                    If File.Exists(sFile) Then
                        Dim psi As New ProcessStartInfo(txtDnetcFolder.Text, "-import " & sFile)
                        p.StartInfo = psi
                        p.Start()
                        Thread.Sleep(500)
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
            Catch ex As Exception
                Console.WriteLine(ex.ToString)
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

    Function Clock(ByVal iTime As Long, Optional GiveDays As Boolean = True, Optional GiveWeeks As Boolean = True, Optional MS As Boolean = True, Optional GiveMs As Boolean = True) As String
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
