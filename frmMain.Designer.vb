<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.ToolTip1 = New System.Windows.Forms.ToolTip(Me.components)
        Me.btnImport = New System.Windows.Forms.Button()
        Me.btnBrowseClient = New System.Windows.Forms.Button()
        Me.btnCreate = New System.Windows.Forms.Button()
        Me.btnRefresh = New System.Windows.Forms.Button()
        Me.LblEstTime = New System.Windows.Forms.Label()
        Me.lbFile = New System.Windows.Forms.ListBox()
        Me.txtDnetcFolder = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.numUpDown = New System.Windows.Forms.NumericUpDown()
        Me.OFD = New System.Windows.Forms.OpenFileDialog()
        Me.RbOgr = New System.Windows.Forms.RadioButton()
        Me.RbRc5 = New System.Windows.Forms.RadioButton()
        Me.ProgressBar1 = New System.Windows.Forms.ProgressBar()
        Me.lblBuffers = New System.Windows.Forms.Label()
        Me.bgwFetch = New System.ComponentModel.BackgroundWorker()
        Me.bgwImport = New System.ComponentModel.BackgroundWorker()
        Me.MenuStrip1 = New System.Windows.Forms.MenuStrip()
        Me.mnuOptions = New System.Windows.Forms.ToolStripMenuItem()
        Me.mnuOptionsAimport = New System.Windows.Forms.ToolStripMenuItem()
        Me.MnuOptionsAutoMinClient = New System.Windows.Forms.ToolStripMenuItem()
        Me.mnuOptionsApause = New System.Windows.Forms.ToolStripMenuItem()
        Me.mnuOptionsMerge = New System.Windows.Forms.ToolStripMenuItem()
        Me.mnuTools = New System.Windows.Forms.ToolStripMenuItem()
        Me.mnuToolsVerify = New System.Windows.Forms.ToolStripMenuItem()
        Me.lblStatus = New System.Windows.Forms.Label()
        CType(Me.numUpDown, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.MenuStrip1.SuspendLayout()
        Me.SuspendLayout()
        '
        'btnImport
        '
        Me.btnImport.Enabled = False
        Me.btnImport.Location = New System.Drawing.Point(12, 233)
        Me.btnImport.Margin = New System.Windows.Forms.Padding(4)
        Me.btnImport.Name = "btnImport"
        Me.btnImport.Size = New System.Drawing.Size(142, 28)
        Me.btnImport.TabIndex = 2
        Me.btnImport.Text = "Import buffers"
        Me.ToolTip1.SetToolTip(Me.btnImport, "Import all buffers in list")
        Me.btnImport.UseVisualStyleBackColor = True
        '
        'btnBrowseClient
        '
        Me.btnBrowseClient.Location = New System.Drawing.Point(13, 32)
        Me.btnBrowseClient.Margin = New System.Windows.Forms.Padding(4)
        Me.btnBrowseClient.Name = "btnBrowseClient"
        Me.btnBrowseClient.Size = New System.Drawing.Size(142, 28)
        Me.btnBrowseClient.TabIndex = 5
        Me.btnBrowseClient.Text = "DNETC folder"
        Me.ToolTip1.SetToolTip(Me.btnBrowseClient, "Browse for the dnet client")
        Me.btnBrowseClient.UseVisualStyleBackColor = True
        '
        'btnCreate
        '
        Me.btnCreate.Location = New System.Drawing.Point(12, 112)
        Me.btnCreate.Margin = New System.Windows.Forms.Padding(4)
        Me.btnCreate.Name = "btnCreate"
        Me.btnCreate.Size = New System.Drawing.Size(142, 28)
        Me.btnCreate.TabIndex = 8
        Me.btnCreate.Text = "Create buffers"
        Me.ToolTip1.SetToolTip(Me.btnCreate, "Create different named buffers or cancel a run")
        Me.btnCreate.UseVisualStyleBackColor = True
        '
        'btnRefresh
        '
        Me.btnRefresh.Location = New System.Drawing.Point(12, 198)
        Me.btnRefresh.Margin = New System.Windows.Forms.Padding(4)
        Me.btnRefresh.Name = "btnRefresh"
        Me.btnRefresh.Size = New System.Drawing.Size(142, 28)
        Me.btnRefresh.TabIndex = 9
        Me.btnRefresh.Text = "Refresh list"
        Me.ToolTip1.SetToolTip(Me.btnRefresh, "Refresh the list")
        Me.btnRefresh.UseVisualStyleBackColor = True
        '
        'LblEstTime
        '
        Me.LblEstTime.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.LblEstTime.Location = New System.Drawing.Point(11, 169)
        Me.LblEstTime.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LblEstTime.Name = "LblEstTime"
        Me.LblEstTime.Size = New System.Drawing.Size(142, 25)
        Me.LblEstTime.TabIndex = 16
        Me.LblEstTime.Text = "0:00:00"
        Me.LblEstTime.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        Me.ToolTip1.SetToolTip(Me.LblEstTime, "Estimated end time when fetching")
        '
        'lbFile
        '
        Me.lbFile.FormattingEnabled = True
        Me.lbFile.ItemHeight = 16
        Me.lbFile.Location = New System.Drawing.Point(190, 103)
        Me.lbFile.Margin = New System.Windows.Forms.Padding(4)
        Me.lbFile.Name = "lbFile"
        Me.lbFile.Size = New System.Drawing.Size(187, 164)
        Me.lbFile.TabIndex = 3
        '
        'txtDnetcFolder
        '
        Me.txtDnetcFolder.Location = New System.Drawing.Point(163, 32)
        Me.txtDnetcFolder.Margin = New System.Windows.Forms.Padding(4)
        Me.txtDnetcFolder.Name = "txtDnetcFolder"
        Me.txtDnetcFolder.Size = New System.Drawing.Size(214, 22)
        Me.txtDnetcFolder.TabIndex = 4
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 62)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(229, 16)
        Me.Label1.TabIndex = 6
        Me.Label1.Text = "How many buffers you want to create: "
        '
        'numUpDown
        '
        Me.numUpDown.Location = New System.Drawing.Point(272, 64)
        Me.numUpDown.Margin = New System.Windows.Forms.Padding(4)
        Me.numUpDown.Maximum = New Decimal(New Integer() {5000, 0, 0, 0})
        Me.numUpDown.Minimum = New Decimal(New Integer() {3, 0, 0, 0})
        Me.numUpDown.Name = "numUpDown"
        Me.numUpDown.Size = New System.Drawing.Size(105, 22)
        Me.numUpDown.TabIndex = 7
        Me.numUpDown.Value = New Decimal(New Integer() {10, 0, 0, 0})
        '
        'OFD
        '
        Me.OFD.FileName = "dnetc.exe"
        Me.OFD.Filter = "Application|*exe"
        Me.OFD.Multiselect = True
        '
        'RbOgr
        '
        Me.RbOgr.AutoSize = True
        Me.RbOgr.Location = New System.Drawing.Point(89, 84)
        Me.RbOgr.Margin = New System.Windows.Forms.Padding(4)
        Me.RbOgr.Name = "RbOgr"
        Me.RbOgr.Size = New System.Drawing.Size(58, 20)
        Me.RbOgr.TabIndex = 10
        Me.RbOgr.Text = "OGR"
        Me.RbOgr.UseVisualStyleBackColor = True
        '
        'RbRc5
        '
        Me.RbRc5.AutoSize = True
        Me.RbRc5.Checked = True
        Me.RbRc5.Location = New System.Drawing.Point(16, 84)
        Me.RbRc5.Margin = New System.Windows.Forms.Padding(4)
        Me.RbRc5.Name = "RbRc5"
        Me.RbRc5.Size = New System.Drawing.Size(54, 20)
        Me.RbRc5.TabIndex = 11
        Me.RbRc5.TabStop = True
        Me.RbRc5.Text = "RC5"
        Me.RbRc5.UseVisualStyleBackColor = True
        '
        'ProgressBar1
        '
        Me.ProgressBar1.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.ProgressBar1.Location = New System.Drawing.Point(0, 280)
        Me.ProgressBar1.Margin = New System.Windows.Forms.Padding(4)
        Me.ProgressBar1.Name = "ProgressBar1"
        Me.ProgressBar1.Size = New System.Drawing.Size(397, 12)
        Me.ProgressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous
        Me.ProgressBar1.TabIndex = 12
        '
        'lblBuffers
        '
        Me.lblBuffers.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblBuffers.Location = New System.Drawing.Point(12, 144)
        Me.lblBuffers.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblBuffers.Name = "lblBuffers"
        Me.lblBuffers.Size = New System.Drawing.Size(142, 25)
        Me.lblBuffers.TabIndex = 13
        Me.lblBuffers.Text = "0/0"
        Me.lblBuffers.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'bgwFetch
        '
        Me.bgwFetch.WorkerReportsProgress = True
        Me.bgwFetch.WorkerSupportsCancellation = True
        '
        'bgwImport
        '
        Me.bgwImport.WorkerReportsProgress = True
        Me.bgwImport.WorkerSupportsCancellation = True
        '
        'MenuStrip1
        '
        Me.MenuStrip1.ImageScalingSize = New System.Drawing.Size(20, 20)
        Me.MenuStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.mnuOptions, Me.mnuTools})
        Me.MenuStrip1.Location = New System.Drawing.Point(0, 0)
        Me.MenuStrip1.Name = "MenuStrip1"
        Me.MenuStrip1.Size = New System.Drawing.Size(397, 28)
        Me.MenuStrip1.TabIndex = 14
        Me.MenuStrip1.Text = "MenuStrip1"
        '
        'mnuOptions
        '
        Me.mnuOptions.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.mnuOptionsAimport, Me.MnuOptionsAutoMinClient, Me.mnuOptionsApause, Me.mnuOptionsMerge})
        Me.mnuOptions.Name = "mnuOptions"
        Me.mnuOptions.Size = New System.Drawing.Size(75, 24)
        Me.mnuOptions.Text = "Options"
        '
        'mnuOptionsAimport
        '
        Me.mnuOptionsAimport.AutoToolTip = True
        Me.mnuOptionsAimport.Checked = True
        Me.mnuOptionsAimport.CheckOnClick = True
        Me.mnuOptionsAimport.CheckState = System.Windows.Forms.CheckState.Checked
        Me.mnuOptionsAimport.Name = "mnuOptionsAimport"
        Me.mnuOptionsAimport.Size = New System.Drawing.Size(233, 26)
        Me.mnuOptionsAimport.Text = "Auto import"
        Me.mnuOptionsAimport.ToolTipText = "After fetching, the new buffer will automaticly be imported"
        '
        'MnuOptionsAutoMinClient
        '
        Me.MnuOptionsAutoMinClient.AutoToolTip = True
        Me.MnuOptionsAutoMinClient.CheckOnClick = True
        Me.MnuOptionsAutoMinClient.Name = "MnuOptionsAutoMinClient"
        Me.MnuOptionsAutoMinClient.Size = New System.Drawing.Size(233, 26)
        Me.MnuOptionsAutoMinClient.Text = "Auto minimise client"
        Me.MnuOptionsAutoMinClient.ToolTipText = "Auto minimise Dnet client"
        '
        'mnuOptionsApause
        '
        Me.mnuOptionsApause.Checked = True
        Me.mnuOptionsApause.CheckOnClick = True
        Me.mnuOptionsApause.CheckState = System.Windows.Forms.CheckState.Checked
        Me.mnuOptionsApause.Name = "mnuOptionsApause"
        Me.mnuOptionsApause.Size = New System.Drawing.Size(233, 26)
        Me.mnuOptionsApause.Text = "Auto (un)pause client"
        '
        'mnuOptionsMerge
        '
        Me.mnuOptionsMerge.AutoToolTip = True
        Me.mnuOptionsMerge.Checked = True
        Me.mnuOptionsMerge.CheckOnClick = True
        Me.mnuOptionsMerge.CheckState = System.Windows.Forms.CheckState.Checked
        Me.mnuOptionsMerge.Name = "mnuOptionsMerge"
        Me.mnuOptionsMerge.Size = New System.Drawing.Size(233, 26)
        Me.mnuOptionsMerge.Text = "Merge into one buffer"
        Me.mnuOptionsMerge.ToolTipText = "Splice every fetch into a single large buffer file, so only one import is needed"
        '
        'mnuTools
        '
        Me.mnuTools.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.mnuToolsVerify})
        Me.mnuTools.Name = "mnuTools"
        Me.mnuTools.Size = New System.Drawing.Size(58, 24)
        Me.mnuTools.Text = "Tools"
        '
        'mnuToolsVerify
        '
        Me.mnuToolsVerify.AutoToolTip = True
        Me.mnuToolsVerify.Name = "mnuToolsVerify"
        Me.mnuToolsVerify.Size = New System.Drawing.Size(233, 26)
        Me.mnuToolsVerify.Text = "Verify selected buffer"
        Me.mnuToolsVerify.ToolTipText = "Check record count and every record checksum of the selected buffer"
        '
        'lblStatus
        '
        Me.lblStatus.AutoEllipsis = True
        Me.lblStatus.Location = New System.Drawing.Point(12, 287)
        Me.lblStatus.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Size = New System.Drawing.Size(373, 18)
        Me.lblStatus.TabIndex = 17
        Me.lblStatus.Text = ""
        '
        'FrmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(397, 318)
        Me.Controls.Add(Me.lblStatus)
        Me.Controls.Add(Me.LblEstTime)
        Me.Controls.Add(Me.lblBuffers)
        Me.Controls.Add(Me.ProgressBar1)
        Me.Controls.Add(Me.RbRc5)
        Me.Controls.Add(Me.RbOgr)
        Me.Controls.Add(Me.btnRefresh)
        Me.Controls.Add(Me.btnCreate)
        Me.Controls.Add(Me.numUpDown)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.btnBrowseClient)
        Me.Controls.Add(Me.txtDnetcFolder)
        Me.Controls.Add(Me.lbFile)
        Me.Controls.Add(Me.btnImport)
        Me.Controls.Add(Me.MenuStrip1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.MainMenuStrip = Me.MenuStrip1
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.MaximizeBox = False
        Me.Name = "FrmMain"
        Me.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide
        Me.Text = "DNETC Large Buffer Creator"
        CType(Me.numUpDown, System.ComponentModel.ISupportInitialize).EndInit()
        Me.MenuStrip1.ResumeLayout(False)
        Me.MenuStrip1.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents ToolTip1 As System.Windows.Forms.ToolTip
    Friend WithEvents btnImport As System.Windows.Forms.Button
    Friend WithEvents lbFile As System.Windows.Forms.ListBox
    Friend WithEvents txtDnetcFolder As System.Windows.Forms.TextBox
    Friend WithEvents btnBrowseClient As System.Windows.Forms.Button
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents numUpDown As System.Windows.Forms.NumericUpDown
    Friend WithEvents btnCreate As System.Windows.Forms.Button
    Friend WithEvents OFD As System.Windows.Forms.OpenFileDialog
    Friend WithEvents btnRefresh As System.Windows.Forms.Button
    Friend WithEvents RbOgr As System.Windows.Forms.RadioButton
    Friend WithEvents RbRc5 As System.Windows.Forms.RadioButton
    Friend WithEvents ProgressBar1 As System.Windows.Forms.ProgressBar
    Friend WithEvents lblBuffers As Label
    Friend WithEvents bgwFetch As System.ComponentModel.BackgroundWorker
    Friend WithEvents bgwImport As System.ComponentModel.BackgroundWorker
    Friend WithEvents MenuStrip1 As MenuStrip
    Friend WithEvents mnuOptions As ToolStripMenuItem
    Friend WithEvents mnuOptionsAimport As ToolStripMenuItem
	Friend WithEvents LblEstTime As Label
	Friend WithEvents MnuOptionsAutoMinClient As ToolStripMenuItem
	Friend WithEvents mnuOptionsApause As ToolStripMenuItem
    Friend WithEvents mnuOptionsMerge As ToolStripMenuItem
    Friend WithEvents mnuTools As ToolStripMenuItem
    Friend WithEvents mnuToolsVerify As ToolStripMenuItem
    Friend WithEvents lblStatus As Label
End Class
