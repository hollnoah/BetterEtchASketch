'Noah Holloway
'RCET 3371
'Fall 2025
'Better Etch-A-Sketch

Imports System.IO.Ports
Imports System.Drawing.Drawing2D
Imports System.Media

Public Class BetterEtchASketch
    ' Current drawing color
    Private currentColor As Color = Color.Black

    ' Graphics object for drawing
    Private g As Graphics

    ' For drawing lines
    Private penPosition As Point

    ' Serial port for Quiet Board
    Private WithEvents PICSerialPort As New SerialPort()

    ' Timers for sending requests and reading data
    Private WithEvents CommandTimer As New Timer()
    Private WithEvents ReadTimer As New Timer()

    ' Mouse drawing flag
    Private isDrawing As Boolean = False

    ' ---------------- Form Load ----------------
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        g = DisplayPictureBox.CreateGraphics()
        DisplayPictureBox.BackColor = Color.White

        PopulatePorts()

        ' Initialize timers
        CommandTimer.Interval = 50   ' request analog every 50ms
        ReadTimer.Interval = 10      ' read buffer every 10ms

        ' Tooltips
        ControlToolTip.SetToolTip(DisplayPictureBox, "Drawing area")
        ControlToolTip.SetToolTip(MouseModeRadioButton, "Draw with mouse")
        ControlToolTip.SetToolTip(ExternalModeRadioButton, "Draw with PIC potentiometers")
        ControlToolTip.SetToolTip(SelectColorButton, "Select drawing color")
        ControlToolTip.SetToolTip(DrawWaveformsButton, "Draw waveforms")
        ControlToolTip.SetToolTip(ClearButton, "Clear display")
        ControlToolTip.SetToolTip(ExitButton, "Exit program")
    End Sub

    ' ---------------- Populate COM Ports ----------------
    Private Sub PopulatePorts()
        PortComboBox.Items.Clear()
        PortComboBox.Items.AddRange(SerialPort.GetPortNames())
        If PortComboBox.Items.Count > 0 Then PortComboBox.SelectedIndex = 0
    End Sub

    Private Sub PortComboBox_DropDown(sender As Object, e As EventArgs) Handles PortComboBox.DropDown
        PopulatePorts()
    End Sub

    ' ---------------- Connect to Quiet Board ----------------
    Private Sub ConnectButton_Click(sender As Object, e As EventArgs) Handles ConnectButton.Click
        If PortComboBox.SelectedItem Is Nothing Then
            MessageBox.Show("Select a COM port first")
            Return
        End If

        Try
            If PICSerialPort.IsOpen Then PICSerialPort.Close()
            PICSerialPort.PortName = PortComboBox.SelectedItem.ToString()
            PICSerialPort.BaudRate = 9600
            PICSerialPort.DataBits = 8
            PICSerialPort.Parity = Parity.None
            PICSerialPort.StopBits = StopBits.One
            PICSerialPort.Open()
            MessageBox.Show($"Connected to {PICSerialPort.PortName}")

            If ExternalModeRadioButton.Checked Then
                CommandTimer.Start()
                ReadTimer.Start()
            End If

        Catch ex As Exception
            MessageBox.Show($"Failed to open port: {ex.Message}")
        End Try
    End Sub

    ' ---------------- Mouse Drawing ----------------
    Private Sub DisplayPictureBox_MouseDown(sender As Object, e As MouseEventArgs) Handles DisplayPictureBox.MouseDown
        If MouseModeRadioButton.Checked AndAlso e.Button = MouseButtons.Left Then
            isDrawing = True
            penPosition = e.Location
        ElseIf e.Button = MouseButtons.Middle Then
            SelectColor()
        End If
    End Sub

    Private Sub DisplayPictureBox_MouseMove(sender As Object, e As MouseEventArgs) Handles DisplayPictureBox.MouseMove
        If isDrawing AndAlso MouseModeRadioButton.Checked Then
            g.DrawLine(New Pen(currentColor), penPosition, e.Location)
            penPosition = e.Location
        End If
    End Sub

    Private Sub DisplayPictureBox_MouseUp(sender As Object, e As MouseEventArgs) Handles DisplayPictureBox.MouseUp
        isDrawing = False
    End Sub

    ' ---------------- External Mode Drawing ----------------
    Private Sub ExternalModeRadioButton_CheckedChanged(sender As Object, e As EventArgs) Handles ExternalModeRadioButton.CheckedChanged
        If ExternalModeRadioButton.Checked Then
            penPosition = New Point(DisplayPictureBox.Width \ 2, DisplayPictureBox.Height \ 2)
            If PICSerialPort.IsOpen Then
                CommandTimer.Start()
                ReadTimer.Start()
            End If
        Else
            CommandTimer.Stop()
            ReadTimer.Stop()
        End If
    End Sub

    ' ---------------- Command Timer ----------------
    Private Sub CommandTimer_Tick(sender As Object, e As EventArgs) Handles CommandTimer.Tick
        If PICSerialPort.IsOpen AndAlso ExternalModeRadioButton.Checked Then
            Const RequestAnalogCommand As Byte = &H53
            PICSerialPort.Write(New Byte() {RequestAnalogCommand}, 0, 1)
        End If
    End Sub

    ' ---------------- Read Timer ----------------
    Private Sub ReadTimer_Tick(sender As Object, e As EventArgs) Handles ReadTimer.Tick
        If Not PICSerialPort.IsOpen Then Exit Sub
        While PICSerialPort.BytesToRead >= 4
            Dim b(3) As Byte
            Dim read As Integer = PICSerialPort.Read(b, 0, 4)
            If read < 4 Then Exit While
            ProcessQuietBoardPacket(b)
        End While
    End Sub

    ' ---------------- Process 4-byte Quiet Board packet ----------------
    Private Sub ProcessQuietBoardPacket(b() As Byte)
        Dim x As Integer = (CInt(b(0)) << 2) Or (b(1) >> 6)
        Dim y As Integer = (CInt(b(2)) << 2) Or (b(3) >> 6)

        Dim mappedX As Integer = CInt(x * DisplayPictureBox.Width / 1023)
        Dim mappedY As Integer = CInt(y * DisplayPictureBox.Height / 1023)
        Dim newPoint As New Point(mappedX, mappedY)

        If ExternalModeRadioButton.Checked Then
            Me.Invoke(Sub()
                          g.DrawLine(New Pen(currentColor), penPosition, newPoint)
                          penPosition = newPoint
                      End Sub)
        Else
            penPosition = newPoint
        End If
    End Sub

    ' ---------------- Color Selection ----------------
    Private Sub SelectColorButton_Click(sender As Object, e As EventArgs) Handles SelectColorButton.Click
        SelectColor()
    End Sub

    Private Sub SelectColorMenuItem_Click(sender As Object, e As EventArgs) Handles SelectColorMenuItem.Click
        SelectColor()
    End Sub

    Private Sub SelectColor()
        Using dlg As New ColorDialog()
            If dlg.ShowDialog() = DialogResult.OK Then
                currentColor = dlg.Color
            End If
        End Using
    End Sub

    ' ---------------- Clear Display ----------------
    Private Sub ClearButton_Click(sender As Object, e As EventArgs) Handles ClearButton.Click
        ShakeDisplay()
        g.Clear(Color.White)
    End Sub

    Private Sub ClearMenuItem_Click(sender As Object, e As EventArgs) Handles ClearMenuItem.Click
        ClearButton.PerformClick()
    End Sub

    Private Sub ShakeDisplay()
        Dim originalLocation As Point = DisplayPictureBox.Location
        For i As Integer = 1 To 10
            DisplayPictureBox.Location = New Point(originalLocation.X + If(i Mod 2 = 0, 5, -5), originalLocation.Y)
            Me.Refresh()
            Threading.Thread.Sleep(20)
        Next
        DisplayPictureBox.Location = originalLocation
    End Sub

    ' ---------------- Draw Waveforms ----------------
    Private Sub DrawWaveformsButton_Click(sender As Object, e As EventArgs) Handles DrawWaveformsButton.Click
        g.Clear(Color.White)
        DrawGraticule()
        DrawSineCosTan()
    End Sub

    Private Sub DrawWaveformsMenuItem_Click(sender As Object, e As EventArgs) Handles DrawWaveformsMenuItem.Click
        DrawWaveformsButton.PerformClick()
    End Sub

    Private Sub DrawGraticule()
        Dim pen As New Pen(Color.LightGray)
        Dim cellWidth As Integer = DisplayPictureBox.Width \ 10
        Dim cellHeight As Integer = DisplayPictureBox.Height \ 10

        For i As Integer = 0 To 10
            g.DrawLine(pen, i * cellWidth, 0, i * cellWidth, DisplayPictureBox.Height)
            g.DrawLine(pen, 0, i * cellHeight, DisplayPictureBox.Width, i * cellHeight)
        Next
    End Sub

    Private Sub DrawSineCosTan()
        Dim w As Integer = DisplayPictureBox.Width
        Dim h As Integer = DisplayPictureBox.Height
        Dim penSine As New Pen(Color.Red)
        Dim penCos As New Pen(Color.Blue)
        Dim penTan As New Pen(Color.Green)

        For i As Integer = 0 To w - 2
            Dim xVal As Double = 2 * Math.PI * i / w
            Dim ySine As Integer = CInt(h / 2 - (h / 4) * Math.Sin(xVal))
            Dim yCos As Integer = CInt(h / 2 - (h / 4) * Math.Cos(xVal))
            Dim yTan As Integer = CInt(h / 2 - (h / 8) * Math.Tan(xVal))
            yTan = Math.Max(0, Math.Min(h - 1, yTan))
            g.DrawLine(penSine, i, ySine, i + 1, CInt(h / 2 - (h / 4) * Math.Sin(2 * Math.PI * (i + 1) / w)))
            g.DrawLine(penCos, i, yCos, i + 1, CInt(h / 2 - (h / 4) * Math.Cos(2 * Math.PI * (i + 1) / w)))
            g.DrawLine(penTan, i, yTan, i + 1, CInt(h / 2 - (h / 8) * Math.Tan(2 * Math.PI * (i + 1) / w)))
        Next
    End Sub

    ' ---------------- Exit ----------------
    Private Sub ExitButton_Click(sender As Object, e As EventArgs) Handles ExitButton.Click
        Me.Close()
    End Sub

    Private Sub ExitMenuItem_Click(sender As Object, e As EventArgs) Handles ExitMenuItem.Click
        ExitButton.PerformClick()
    End Sub

    ' ---------------- About ----------------
    Private Sub AboutMenuItem_Click(sender As Object, e As EventArgs) Handles AboutMenuItem.Click
        MessageBox.Show("Better Etch-A-Sketch v1.0" & vbCrLf & "Created for PIC Assignment", "About")
    End Sub
End Class


