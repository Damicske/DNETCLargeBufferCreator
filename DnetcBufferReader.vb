Option Strict Off
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text

Public Class DnetcBufferFile
    Public Const HeaderSize As Integer = &H20
    Public Const RecordSize As Integer = &HB0
    Public Const RecordWordCount As Integer = 44

    Private Const MagicExpected As UInteger = &H83B6341AUI
    Private Const VersionUnlocked As UInteger = &H48UI
    Private Const VersionLocked As UInteger = &H49UI
    Friend Const RC5_P As UInteger = &HB7E15163UI
    Friend Const NegQ As UInteger = &H61C88647UI
    Friend Const ScrambleStep As UInteger = &H481EAE9DUI

    Public Property Magic As UInteger
    Public Property IsLocked As Boolean
    Public Property RecordCount As UInteger
    Public Property StatsUnitsCount As UInteger
    Public Property Reserved As Byte()
    Public Property Records As New List(Of DnetcBufferRecord)

    Public Shared Function Load(path As String) As DnetcBufferFile
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read)
            Return Load(fs)
        End Using
    End Function

    Public Shared Function Load(stream As Stream) As DnetcBufferFile
        Dim result As New DnetcBufferFile()
        Dim headerBytes(HeaderSize - 1) As Byte
        If ReadFully(stream, headerBytes, HeaderSize) <> HeaderSize Then
            Throw New InvalidDataException("File is smaller than the 32-byte buffer header.")
        End If

        Dim magicRaw As UInteger = BitConverter.ToUInt32(headerBytes, 0)
        Dim versionRaw As UInteger = BitConverter.ToUInt32(headerBytes, 4)
        Dim countRaw As UInteger = BitConverter.ToUInt32(headerBytes, 8)

        result.Magic = ByteSwap32(magicRaw)
        Dim versionSwapped As UInteger = ByteSwap32(versionRaw)
        result.RecordCount = ByteSwap32(countRaw)
        result.Reserved = New Byte(19) {}
        Array.Copy(headerBytes, 12, result.Reserved, 0, 20)

        If result.Magic <> MagicExpected Then
            Throw New InvalidDataException(String.Format("Not a distributed.net buffer file (magic was 0x{0:X8}, expected 0x{1:X8}).", result.Magic, MagicExpected))
        End If

        result.IsLocked = (versionSwapped = VersionLocked)

        For i As UInteger = 0 To If(result.RecordCount = 0, 0UI, result.RecordCount - 1UI)
            If result.RecordCount = 0 Then Exit For
            Dim recBytes(RecordSize - 1) As Byte
            Dim got = ReadFully(stream, recBytes, RecordSize)
            If got = 0 Then Exit For
            If got < RecordSize Then
                Throw New InvalidDataException(String.Format("Truncated record #{0} (got {1} of {2} bytes).", i, got, RecordSize))
            End If
            result.Records.Add(DnetcBufferRecord.Parse(recBytes))
            result.StatsUnitsCount += result.Records(i).StatsUnitsInPacket
        Next
        Return result
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

    Friend Shared Function ByteSwap32(v As UInteger) As UInteger
        Return ((v And &HFF000000UI) >> 24) Or ((v And &HFF0000UI) >> 8) Or ((v And &HFF00UI) << 8) Or ((v And &HFFUI) << 24)
    End Function

    Friend Shared Function ModAdd(a As UInteger, b As UInteger) As UInteger
        Return CUInt((CULng(a) + CULng(b)) And &HFFFFFFFFUL)
    End Function

    Friend Shared Function ModSub(a As UInteger, b As UInteger) As UInteger
        Dim result As Long = CLng(a) - CLng(b)
        Return CUInt(result And &HFFFFFFFFL)
    End Function

    Friend Shared Sub DescrambleInPlace(words As UInteger(), count As Integer, seed As UInteger)
        Dim state As UInteger = seed
        For i As Integer = 0 To count - 1
            Dim w As UInteger = ByteSwap32(words(i))
            w = ModAdd(w, NegQ)
            w = Not w
            w = w Xor state
            words(i) = ByteSwap32(w)
            state = ModSub(state, ScrambleStep)
        Next
    End Sub

    Friend Shared Function ComputeChecksum(words As UInteger(), count As Integer) As UInteger
        Dim state As UInteger = RC5_P
        For outerPass As Integer = 1 To 8
            For i As Integer = 0 To count - 1
                Dim w As UInteger = ByteSwap32(words(i))
                w = w Xor state
                state = ModSub(w, NegQ)
            Next
        Next
        Return state
    End Function

    ''' <summary>
    ''' Simple wrapper: loads the file fully (same validated path as Load()),
    ''' then returns just the two numbers most callers actually need. Safe
    ''' default - every record's checksum is still verified, so a corrupted
    ''' record still throws/logs the same way Load() does.
    ''' Note: stats units per record is NOT a fixed 256 - it's only known
    ''' after descrambling each record's word 11 (confirmed varying, e.g.
    ''' 252 vs 256 in real captured data), so there's no header-only
    ''' shortcut for TotalStatsUnits.
    ''' </summary>
    Public Shared Function LoadSummary(path As String) As BufferSummary
        Dim full = Load(path)
        Return New BufferSummary With {
            .PacketCount = full.RecordCount,
            .TotalStatsUnits = full.StatsUnitsCount
        }
    End Function

    ''' <summary>
    ''' Faster path for frequent polling: still reads every record (there is
    ''' no way around that - see LoadSummary remarks), but skips the 8-pass
    ''' checksum computation and skips decoding words unrelated to the
    ''' stats-unit count (email, position, etc). Only decodes words 0..11
    ''' per record (plus word 43, the scramble seed).
    '''
    ''' Trade-off: no checksum validation, so a corrupted record won't be
    ''' caught here the way LoadSummary()/Load() would catch it - use
    ''' LoadSummary() instead if you want that safety and don't need the
    ''' extra speed.
    ''' </summary>
    Public Shared Function LoadSummaryFast(path As String) As BufferSummary
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Dim headerBytes(HeaderSize - 1) As Byte
            If ReadFully(fs, headerBytes, HeaderSize) <> HeaderSize Then
                Throw New InvalidDataException("File is smaller than the 32-byte buffer header.")
            End If

            Dim magic = ByteSwap32(BitConverter.ToUInt32(headerBytes, 0))
            If magic <> MagicExpected Then
                Throw New InvalidDataException(
                    String.Format("Not a distributed.net buffer file (magic was 0x{0:X8}, expected 0x{1:X8}).",
                                  magic, MagicExpected))
            End If

            Dim count = ByteSwap32(BitConverter.ToUInt32(headerBytes, 8))
            Dim total As ULong = 0

            For i As UInteger = 1 To count
                Dim recBytes(RecordSize - 1) As Byte
                Dim got = ReadFully(fs, recBytes, RecordSize)
                If got < RecordSize Then Exit For   ' truncated/mid-write - stop quietly, same as Load()

                Dim words(RecordWordCount - 1) As UInteger
                For w As Integer = 0 To RecordWordCount - 1
                    words(w) = BitConverter.ToUInt32(recBytes, w * 4)
                Next

                Dim state As UInteger = ByteSwap32(words(43))   ' scramble seed
                Dim statsUnits As UInteger = 0

                ' Only need to walk the sequential descramble state through word 11 -
                ' each word's transform only depends on the running state, not on
                ' any other word's value, so we can stop right after index 11.
                For idx As Integer = 0 To 11
                    Dim w As UInteger = ByteSwap32(words(idx))
                    w = ModAdd(w, NegQ)
                    w = Not w
                    w = w Xor state
                    If idx = 11 Then statsUnits = w   ' this IS the host-order value already
                    state = ModSub(state, ScrambleStep)
                Next

                total += statsUnits
            Next

            Return New BufferSummary With {.PacketCount = count, .TotalStatsUnits = total}
        End Using
    End Function
End Class

''' <summary>
''' Result of a summary-only load: just packet count and total stats units,
''' no per-record detail (no email, checksum, position, etc).
''' </summary>
Public Class BufferSummary
    Public Property PacketCount As UInteger
    Public Property TotalStatsUnits As ULong
End Class


''' <summary>
''' CLIENT_CPU values, confirmed from common/cputypes.h in the dnetc-client-base
''' public source. "There are no longer any size limitations storing CLIENT_CPU
''' and CLIENT_OS in the buffer files" per the header's own comment.
''' </summary>
Public Enum ClientCpu As UInteger
    Unknown = 0
    X86 = 1
    PowerPC = 2
    Mips = 3
    Alpha = 4
    PaRisc = 5
    M68K = 6
    Sparc = 7
    Sh4 = 8
    Power = 9
    Vax = 10
    Arm = 11
    M88K = 12
    Ia64 = 13
    S390 = 14
    S390X = 15
    Descracker = 16
    Amd64 = 17
    CellBe = 18
    Cuda = 19
    AtiStream = 20
    OpenCl = 21
    Arm64 = 22
    Ppc64 = 23
End Enum

''' <summary>
''' CLIENT_OS values, confirmed from common/cputypes.h. Several numbers are
''' intentionally retired/unused ("DO NOT RECYCLE OLD OS SLOTS" per the header)
''' and are simply absent below.
''' </summary>
Public Enum ClientOs As UInteger
    Unknown = 0
    Win32 = 1
    Dos = 2
    FreeBsd = 3
    Linux = 4
    BeOs = 5
    Irix = 7
    Vms = 8
    DecUnix = 9
    UnixWare = 10
    Os2 = 11
    HpUx = 12
    NetBsd = 13
    SunOs = 14
    Solaris = 15
    BsdOs = 18
    NextStep = 19
    Sco = 20
    Qnx = 21
    Aix = 25
    MacOsX = 27
    AmigaOs = 28
    OpenBsd = 29
    NetWare = 30
    Mvs = 31
    Ultrix = 32
    RiscOs = 34
    DgUx = 35
    Sinix = 37
    Dynix = 38
    Os390 = 39
    Win16 = 41
    Descracker = 42
    Ps2Linux = 44
    MorphOs = 45
    Win64 = 46
    NetWare6 = 47
    DragonFly = 48
    Haiku = 49
    Android = 50
    Ios = 51
End Enum

Public Class DnetcBufferRecord
    Public Property RawWords As UInteger()
    Public Property HostWords As UInteger()
    Public Property ChecksumValid As Boolean
    Public Property StoredChecksum As UInteger
    Public Property ComputedChecksum As UInteger
    Public Property ScrambleSeed As UInteger
    Public Property StatsUnits As UInteger

    Public ReadOnly Property FormatVersion As UInteger
        Get
            Return HostWords(37)
        End Get
    End Property

    Public ReadOnly Property TypeCode As UInteger
        Get
            Dim raw = HostWords(20)
            Select Case raw
                Case &H66UI, 2UI
                    Return 2UI
                Case 3UI, &H67UI, &H6BUI
                    Return 1UI 'is done
                Case Else
                    Return 0UI
            End Select
        End Get
    End Property

    Public ReadOnly Property IsDone As Boolean
        Get
            Return TypeCode = (HostWords(20) = 1UI)
        End Get
    End Property
    Public ReadOnly Property IsNew As Boolean
        Get
            Return TypeCode = (HostWords(20) = 0UI)
        End Get
    End Property
    Friend Shared Function Parse(bytes As Byte()) As DnetcBufferRecord
        If bytes.Length <> DnetcBufferFile.RecordSize Then
            Throw New ArgumentException(String.Format("Record must be exactly {0} bytes.", DnetcBufferFile.RecordSize))
        End If

        Dim rec As New DnetcBufferRecord()
        Dim words(DnetcBufferFile.RecordWordCount - 1) As UInteger
        For i As Integer = 0 To DnetcBufferFile.RecordWordCount - 1
            words(i) = BitConverter.ToUInt32(bytes, i * 4)
        Next
        rec.RawWords = words

        Dim work(DnetcBufferFile.RecordWordCount - 1) As UInteger
        Array.Copy(words, work, words.Length)

        rec.ScrambleSeed = DnetcBufferFile.ByteSwap32(words(43))
        DnetcBufferFile.DescrambleInPlace(work, 43, rec.ScrambleSeed)

        rec.ComputedChecksum = DnetcBufferFile.ComputeChecksum(work, 42)
        rec.StoredChecksum = DnetcBufferFile.ByteSwap32(work(42))
        rec.ChecksumValid = (rec.ComputedChecksum = rec.StoredChecksum)

        Dim host(DnetcBufferFile.RecordWordCount - 1) As UInteger
        For i As Integer = 0 To 41
            host(i) = DnetcBufferFile.ByteSwap32(work(i))
        Next

        host(42) = rec.StoredChecksum
        host(43) = rec.ScrambleSeed
        rec.StatsUnits = host(11)
        rec.HostWords = host

        Return rec
    End Function

    Public ReadOnly Property Email As String
        Get
            ' Email spans words 21-36 (64 bytes total)
            Dim emailBytes(63) As Byte
            For i As Integer = 0 To 15
                Dim word = HostWords(21 + i)
                emailBytes(i * 4 + 0) = CByte((word >> 24) And &HFF)
                emailBytes(i * 4 + 1) = CByte((word >> 16) And &HFF)
                emailBytes(i * 4 + 2) = CByte((word >> 8) And &HFF)
                emailBytes(i * 4 + 3) = CByte(word And &HFF)
            Next
            Return Encoding.ASCII.GetString(emailBytes).TrimEnd(ChrW(0))
        End Get
    End Property

    Public ReadOnly Property CoreId As UInteger
        Get
            Return HostWords(41)
        End Get
    End Property

    Public ReadOnly Property StartAddress As String
        Get
            Return HostWords(0).ToString("X2") & ":" & HostWords(1).ToString("X8") & ":" & HostWords(2).ToString("X8")
        End Get
    End Property

    Public ReadOnly Property StatsUnitsDone As UInteger
        Get
            Return HostWords(9)
        End Get
    End Property

    Public ReadOnly Property StatsUnitsInPacket As UInteger
        Get
            Return HostWords(11)
        End Get
    End Property

    ''' <summary>Word 3-4: IV (initialization vector), confirmed via problem.cpp/problem.h (ContestWork.bigcrypto.iv).</summary>
    Public ReadOnly Property IvHi As UInteger
        Get
            Return HostWords(3)
        End Get
    End Property
    Public ReadOnly Property IvLo As UInteger
        Get
            Return HostWords(4)
        End Get
    End Property

    ''' <summary>Word 5-6: plaintext being searched for (ContestWork.bigcrypto.plain).</summary>
    Public ReadOnly Property PlainHi As UInteger
        Get
            Return HostWords(5)
        End Get
    End Property
    Public ReadOnly Property PlainLo As UInteger
        Get
            Return HostWords(6)
        End Get
    End Property

    ''' <summary>Word 7-8: ciphertext being matched against (ContestWork.bigcrypto.cypher).</summary>
    Public ReadOnly Property CypherHi As UInteger
        Get
            Return HostWords(7)
        End Get
    End Property
    Public ReadOnly Property CypherLo As UInteger
        Get
            Return HostWords(8)
        End Get
    End Property

    ''' <summary>Word 9: keysdone.hi - real 64-bit progress counter (high word). Equal to
    ''' StatsUnitsDone; kept as a clearer alias matching the confirmed struct field name.</summary>
    Public ReadOnly Property KeysDoneHi As UInteger
        Get
            Return HostWords(9)
        End Get
    End Property

    ''' <summary>Word 10: keysdone.lo - low word of the real 64-bit progress counter.
    ''' Confirmed via problem.cpp (ContestWork.bigcrypto.keysdone); varies continuously as
    ''' work progresses (the old "only 0x20 or 0x00" note on Word10Value was based on too
    ''' small a sample and is not accurate - see Word10Value remarks).</summary>
    Public ReadOnly Property KeysDoneLo As UInteger
        Get
            Return HostWords(10)
        End Get
    End Property

    ''' <summary>Word 12: iterations.lo - low word of the 64-bit total range. Expected to be
    ''' 0 for standard N*2^32-aligned blocks (StatsUnitsInPacket/iterations.hi covers those);
    ''' only meaningful for irregular/non-aligned blocks.</summary>
    Public ReadOnly Property IterationsLo As UInteger
        Get
            Return HostWords(12)
        End Get
    End Property

    Public ReadOnly Property RandomSubspaceId As UInteger
        Get
            Return HostWords(13)
        End Get
    End Property

    ''' <summary>Word 14: check.count - part of the "counter-measure check" anti-cheat field
    ''' (ContestWork.bigcrypto.check), not a general "completion value". Kept as an alias;
    ''' see CheckCount for the clearer name.</summary>
    Public ReadOnly Property CompletionValue As UInteger
        Get
            Return HostWords(14)
        End Get
    End Property

    ''' <summary>Word 14: check.count - "keyid of last found counter-measure check" per
    ''' problem.h. Same value as CompletionValue, clearer name.</summary>
    Public ReadOnly Property CheckCount As UInteger
        Get
            Return HostWords(14)
        End Get
    End Property

    ''' <summary>Words 15-17: check.hi/mid/lo - the rest of the counter-measure/anti-cheat
    ''' field (ContestWork.bigcrypto.check). NOT a live key position - see CurrentPosition
    ''' remarks, which used these same words under an incorrect assumption.</summary>
    Public ReadOnly Property CheckHi As UInteger
        Get
            Return HostWords(15)
        End Get
    End Property
    Public ReadOnly Property CheckMid As UInteger
        Get
            Return HostWords(16)
        End Get
    End Property
    Public ReadOnly Property CheckLo As UInteger
        Get
            Return HostWords(17)
        End Get
    End Property

    ''' <summary>Word 10, real meaning is keysdone.lo (see KeysDoneLo). The original note
    ''' that this "is only set to 0x20 or 0x00" was based on a very small sample (mostly
    ''' untouched/zero records) and is not accurate in general - it's the low word of a
    ''' 64-bit progress counter and should vary continuously as work progresses. Kept as an
    ''' alias for backward compatibility; prefer KeysDoneLo in new code.</summary>
    Public ReadOnly Property Word10Value As UInteger
        Get
            Return HostWords(10)
        End Get
    End Property

    ''' <summary>Word 38: cpu - CLIENT_CPU enum of the client that last ran this record.
    ''' Confirmed via problem.cpp: compared against the running client's cpu type on load,
    ''' and keysdone is reset to 0 if it doesn't match (work never resumes on a different
    ''' machine/core/build). Zero on records that have never been run by any client.</summary>
    Public ReadOnly Property Cpu As UInteger
        Get
            Return HostWords(38)
        End Get
    End Property

    ''' <summary>Cpu decoded against the confirmed ClientCpu enum (from cputypes.h),
    ''' e.g. "OpenCl". Falls back to "Unknown (N)" for any value not in the table.</summary>
    Public ReadOnly Property CpuName As String
        Get
            If [Enum].IsDefined(GetType(ClientCpu), Cpu) Then
                Return [Enum].GetName(GetType(ClientCpu), Cpu)
            End If
            Return $"Unknown ({Cpu})"
        End Get
    End Property

    ''' <summary>Word 39: os - CLIENT_OS enum. See Cpu remarks; same reset-on-mismatch behavior.</summary>
    Public ReadOnly Property Os As UInteger
        Get
            Return HostWords(39)
        End Get
    End Property

    ''' <summary>Os decoded against the confirmed ClientOs enum (from cputypes.h),
    ''' e.g. "Win32". Falls back to "Unknown (N)" for any value not in the table.</summary>
    Public ReadOnly Property OsName As String
        Get
            If [Enum].IsDefined(GetType(ClientOs), Os) Then
                Return [Enum].GetName(GetType(ClientOs), Os)
            End If
            Return $"Unknown ({Os})"
        End Get
    End Property

    ''' <summary>Word 40: build - CLIENT_VERSION combined build identifier. See Cpu remarks;
    ''' same reset-on-mismatch behavior. Observed to embed the client's version string
    ''' directly (e.g. 91120521 for client version "2.9112-521").</summary>
    Public ReadOnly Property Build As UInteger
        Get
            Return HostWords(40)
        End Get
    End Property

    Public ReadOnly Property PercentComplete As Double
        Get
            Try
                ' Use StatsUnitsDone / StatsUnitsInPacket for reliable percentage
                Dim unitsDone As ULong = CULng(StatsUnitsDone)
                Dim unitsTotal As ULong = CULng(StatsUnitsInPacket)
                If unitsTotal = 0 Then Return 0.0
                Dim percentage As Double = (CDbl(unitsDone) / CDbl(unitsTotal)) * 100.0
                ' Clamp to 0-100 range
                If percentage < 0 Then percentage = 0
                If percentage > 100 Then percentage = 100
                Return Math.Round(percentage, 2)
            Catch ex As Exception
                Return 0.0
            End Try
        End Get
    End Property

    ''' <summary>
    ''' OBSOLETE / INCORRECT: was written under the assumption that words 15-17 hold a
    ''' live key position. problem.h/problem.cpp confirm those words are actually
    ''' check.hi/mid/lo - the counter-measure/anti-cheat field - not a position. Kept only
    ''' to avoid breaking existing callers; do not rely on this value. Use KeysDoneHi/Lo
    ''' with StartAddress instead for a real current position.
    ''' </summary>
    <Obsolete("Incorrect: words 15-17 are check.hi/mid/lo, not a key position. Use KeysDoneHi/KeysDoneLo instead.")>
    Public ReadOnly Property CurrentPosition As String
        Get
            Return HostWords(15).ToString("X8") & ":" & HostWords(16).ToString("X8") & ":" & HostWords(17).ToString("X8")
        End Get
    End Property

    Public ReadOnly Property TotalRange As ULong
        Get
            Return CULng(StatsUnitsInPacket) * &H100000000UL
        End Get
    End Property

    ''' <summary>
    ''' OBSOLETE / INCORRECT: see CurrentPosition remarks - words 15/17 used here are
    ''' actually check.hi/lo (anti-cheat field), not a live position, so this does not
    ''' compute a real completed-range value. Kept only to avoid breaking existing callers.
    ''' For a correct completed range, use KeysDoneHi/KeysDoneLo directly:
    '''   completedRange = (CULng(KeysDoneHi) &lt;&lt; 32) Or CULng(KeysDoneLo)
    ''' </summary>
    <Obsolete("Incorrect: uses check.hi/lo, not a key position. Use (KeysDoneHi << 32) Or KeysDoneLo instead.")>
    Public ReadOnly Property CompletedRange As ULong
        Get
            Try
                Dim startPosition As ULong = (CULng(HostWords(1)) << 32) Or CULng(HostWords(2))
                Dim currentPosition As ULong = (CULng(HostWords(15)) << 32) Or CULng(HostWords(17))
                Return currentPosition - startPosition
            Catch ex As Exception
                Return 0UL
            End Try
        End Get
    End Property

    Public Function GetWordMap() As Dictionary(Of Integer, String)
        Dim wordMap As New Dictionary(Of Integer, String)
        wordMap.Add(0, "key.hi - Start Address High")
        wordMap.Add(1, "key.mid - Start Address Mid")
        wordMap.Add(2, "key.lo - Start Address Low")
        wordMap.Add(3, "iv.hi")
        wordMap.Add(4, "iv.lo")
        wordMap.Add(5, "plain.hi")
        wordMap.Add(6, "plain.lo")
        wordMap.Add(7, "cypher.hi")
        wordMap.Add(8, "cypher.lo")
        wordMap.Add(9, "keysdone.hi - Stats Units Done")
        wordMap.Add(10, "keysdone.lo (varies continuously as work progresses)")
        wordMap.Add(11, "iterations.hi - Stats Units In Packet")
        wordMap.Add(12, "iterations.lo (usually 0 for standard N*2^32 blocks)")
        wordMap.Add(13, "randomsubspace (from server)")
        wordMap.Add(14, "check.count - counter-measure/anti-cheat field")
        wordMap.Add(15, "check.hi - counter-measure/anti-cheat field")
        wordMap.Add(16, "check.mid - counter-measure/anti-cheat field")
        wordMap.Add(17, "check.lo - counter-measure/anti-cheat field")
        wordMap.Add(18, "padding (union sized for OGR variant, unused for RC5-72)")
        wordMap.Add(19, "padding (union sized for OGR variant, unused for RC5-72)")
        wordMap.Add(20, "resultcode (on-disk encoded; see TypeCode remap)")
        wordMap.Add(21, "id[64] - Email Start (64 bytes across words 21-36)")
        wordMap.Add(37, "contest (Format Version; typically 5 for RC5-72)")
        wordMap.Add(38, "cpu - CLIENT_CPU of the client that last ran this record")
        wordMap.Add(39, "os - CLIENT_OS of the client that last ran this record")
        wordMap.Add(40, "build - CLIENT_VERSION of the client that last ran this record")
        wordMap.Add(41, "core - Core ID")
        wordMap.Add(42, "Stored Checksum")
        wordMap.Add(43, "Scramble Seed")
        Return wordMap
    End Function
End Class