Imports System.Runtime.InteropServices

Public Enum TaskbarState
    NoProgress = 0
    Indeterminate = 1
    Normal = 2
    [Error] = 4
    Paused = 8
End Enum

Public Class TaskBarProgressWrapper
    Private Shared ReadOnly _Instance As ITaskbarList3 = CType(New TaskbarInstance(), ITaskbarList3)
    Private Shared ReadOnly _HasTaskbarSupport As Boolean = Environment.OSVersion.Version >= New Version(6, 1)

    Public Shared Sub SetState(windowHandle As IntPtr, taskbarState As TaskbarState)
        If _HasTaskbarSupport Then _Instance.SetProgressState(windowHandle, taskbarState)
    End Sub

    Public Shared Sub SetValue(windowHandle As IntPtr, progressValue As Integer, progressMax As Integer)
        If _HasTaskbarSupport Then _Instance.SetProgressValue(windowHandle, Convert.ToUInt64(progressValue), Convert.ToUInt32(progressMax))
    End Sub

    <ComImport()>
    <Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface ITaskbarList3

        <PreserveSig>
        Sub HrInit()

        <PreserveSig>
        Sub AddTab(hwnd As IntPtr)

        <PreserveSig>
        Sub DeleteTab(hwnd As IntPtr)

        <PreserveSig>
        Sub ActivateTab(hwnd As IntPtr)

        <PreserveSig>
        Sub SetActiveAlt(hwnd As IntPtr)

        <PreserveSig>
        Sub MarkFullscreenWindow(hwnd As IntPtr, <MarshalAs(UnmanagedType.Bool)> fFullscreen As Boolean)

        <PreserveSig>
        Sub SetProgressValue(hwnd As IntPtr, ullCompleted As UInt64, ullTotal As UInt64)

        <PreserveSig>
        Sub SetProgressState(hwnd As IntPtr, state As TaskbarState)

    End Interface

    <Guid("56FDF344-FD6D-11d0-958A-006097C9A090")>
    <ClassInterface(ClassInterfaceType.None)>
    <ComImport()>
    Private Class TaskbarInstance
    End Class
End Class
