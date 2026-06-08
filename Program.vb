Option Strict On
Option Infer On
Imports System.Diagnostics.CodeAnalysis

Public Module Program
    Private playerPos As New GridIndex(17, 10)
    Private pressedKey As New ConsoleKeyInfo
    Private ReadOnly fruitTimer As New Stopwatch
    Private ReadOnly scaredTimer As New Stopwatch

    Private NotInheritable Class EnemyInfo
        Public Property CurrPos As GridIndex
        Public Property Scared As Boolean
        Public Property Color As ConsoleColor
        Public ReadOnly Property InitialPos As GridIndex

        Public Sub New(pos As GridIndex, scared As Boolean, color As ConsoleColor)
            InitialPos = pos
            CurrPos = pos
            Me.Scared = scared
            Me.Color = color
        End Sub
    End Class

    Private ReadOnly Property InitialEnemyInfo As EnemyInfo() = {
        New EnemyInfo(New GridIndex(10, 10), False, ConsoleColor.Red),
        New EnemyInfo(New GridIndex(11, 9), False, ConsoleColor.Magenta),
        New EnemyInfo(New GridIndex(11, 10), False, ConsoleColor.Cyan),
        New EnemyInfo(New GridIndex(11, 11), False, ConsoleColor.Yellow)
    }

    Private ReadOnly Property InitialGameMap As Char(,)
        Get
            Dim mapContent As String() = {
                "########## ############",
                "#.*....### ####..*#...#",
                "#.##.#.### ####.#.#.#.#",
                "#.##.#.### ####.#...#.#",
                "#.##.#.### ####.###.#.#",
                "#...................#.#",
                "#.##.##### ####.#.###.#",
                "#.##...#       .#...#.#",
                "#.##.#.# #### #.#.#.#.#",
                "#....#.  #  # #...#...#",
                "####.###    # ### ###.#",
                "#....#.  #  # #...#...#",
                "#.##.#.# #### #.#.#.#.#",
                "#.##...#       .#...#.#",
                "#.##.##### ####.#.###.#",
                "#...................#.#",
                "#.##.#.### ####.###.#.#",
                "#.##.#.### ####.#...#.#",
                "#.##.#.### ####.#.#.#.#",
                "#.*....### ####..*#...#",
                "########## ############"
            }

            Dim result(MaxLineIndex(mapContent), UBound(mapContent)) As Char

            For x As Integer = 0 To UBound(result, 1) Step 1
                For y As Integer = 0 To UBound(result, 2) Step 1
                    result(x, y) = mapContent(y)(x)
                Next y
            Next x

            Return result
        End Get
    End Property

    Private isGameInProcess As Boolean = False, isPowerPelletEaten As Boolean = False

    Friend Sub Main()
        Console.Clear()  ' Clear the terminal for the title screen display.
        Console.CursorVisible = False
        Randomize()

        Dim currGameMap As Char(,) = CType(InitialGameMap.Clone(), Char(,))
        Dim currEnemyInfo(3) As EnemyInfo
        Array.Copy(InitialEnemyInfo, currEnemyInfo, InitialEnemyInfo.Length)
        Dim playerScore As New Integer, playerLives As Integer = 3, currLevel As Integer = 1

        Dim GetRandomTarget = Function() As GridIndex
                                  Dim rndX = CInt(Rnd(UBound(InitialGameMap, 1)))
                                  Dim rndY = CInt(Rnd(UBound(InitialGameMap, 2)))
                                  Return New GridIndex(rndX, rndY)
                              End Function

        ' This part of code handles the fixed update during the gameplay.
        Task.Run(Sub()
                     Do
                         pressedKey = Console.ReadKey()
                         If Not isGameInProcess Then Continue Do
                         Dim enemyDirections(3) As GridIndex, targetPos As GridIndex
                         For i As Integer = 0 To UBound(currEnemyInfo) Step 1
                             If currEnemyInfo(i).Scared Then
                                 targetPos = playerPos * (-1) + New GridIndex(UBound(InitialGameMap, 1) \ 2, UBound(InitialGameMap, 2) \ 2) * 2
                             Else
                                 Select Case i
                                     Case 0
                                         targetPos = playerPos
                                     Case 1
                                         targetPos = playerPos - PlayerDirection * 4
                                     Case 2
                                         targetPos = currEnemyInfo(0).CurrPos * 2 - playerPos
                                     Case 3
                                         targetPos = GetRandomTarget()
                                 End Select
                             End If
                             Dim ghostRoute = AStarAlgorithm.FindUniqueRoute(WalkableTerrain,
                                    currEnemyInfo(i).CurrPos, targetPos)

                             If ghostRoute.Count > 0 Then
                                 Dim nextPos = currEnemyInfo(i).CurrPos + ghostRoute(0)
                                 If IsWalkable(nextPos) Then
                                     enemyDirections(i) = ghostRoute(0)
                                 Else
                                     enemyDirections(i) = GetRandomDirection(currEnemyInfo(i).CurrPos)
                                 End If
                             Else
                                 enemyDirections(i) = GetRandomDirection(currEnemyInfo(i).CurrPos)
                             End If
                             Dim newPos = currEnemyInfo(i).CurrPos + enemyDirections(i)
                             If IsWalkable(newPos) Then
                                 currEnemyInfo(i).CurrPos = newPos
                             End If
                             If isPowerPelletEaten Then currEnemyInfo(i).Scared = True
                             If Not scaredTimer.IsRunning Then currEnemyInfo(i).Scared = False
                         Next i
                         If currEnemyInfo.All(Function(enemy) enemy.Scared) Then
                             isPowerPelletEaten = False
                         End If
                     Loop
                 End Sub)

        Do
            If isGameInProcess Then
                GameplayProcess(currGameMap, playerScore, playerLives, currLevel, currEnemyInfo)
            Else
                DisplayTitleScreen()
            End If
        Loop
    End Sub

    <Runtime.CompilerServices.Extension> Private Sub StopAndReset(stopwatch As Stopwatch)
        ' The process of stoping and resetting the in-game timers will be condensed to
        ' this unique extension method.
        With stopwatch : .Stop() : .Reset() : End With
    End Sub

    Private ReadOnly Property WalkableTerrain As Boolean(,)
        Get
            Dim maxRowIdx = UBound(InitialGameMap, 1), maxColIdx = UBound(InitialGameMap, 2)
            Dim terrain(maxRowIdx, maxColIdx) As Boolean
            For i As Integer = 0 To maxRowIdx Step 1
                For j As Integer = 0 To maxColIdx Step 1
                    If InitialGameMap(i, j) <> "#"c Then terrain(i, j) = True
                Next j
            Next i
            Return terrain
        End Get
    End Property

    Private ReadOnly Property TimerThreshold(level As Integer, isForGhost As Boolean) As Double
        Get
            If isForGhost Then
                Return If(level < 5, 12 - level * 1.5, 7) * 1000
            Else
                Return If(level < 5, 8 + level * 1.5, 15) * 1000
            End If
        End Get
    End Property

    Private Sub DisplayTitleScreen()
        Console.ForegroundColor = ConsoleColor.White
        Console.SetCursorPosition(7, 5)
        Console.Write("-* TERMINAL MAZE CHASE *-")
        Console.SetCursorPosition(5, 7)
        Console.Write("Press ""Enter"" to begin the game.")
        If pressedKey.Key = ConsoleKey.Enter Then isGameInProcess = True
    End Sub

    Private Sub GameplayProcess(ByRef gameMap As Char(,), ByRef score%, ByRef lives%,
             ByRef level%, ByRef enemyInfo As EnemyInfo())

        Static bonusMult As New Integer, playerHasExtraLife As Boolean = False
        Console.Clear()

        Dim direction As GridIndex = PlayerDirection
        Dim nextPosX As Integer = playerPos.x + direction.x
        Dim nextPosY As Integer = playerPos.y + direction.y

        If nextPosX > UBound(gameMap, 1) Then nextPosX = 0
        If nextPosX < 0 Then nextPosX = UBound(gameMap, 1)
        If nextPosY > UBound(gameMap, 2) Then nextPosY = 0
        If nextPosY < 0 Then nextPosY = UBound(gameMap, 2)

        If gameMap(nextPosX, nextPosY) <> "#" Then
            playerPos = New GridIndex(nextPosX, nextPosY)

            Select Case gameMap(nextPosX, nextPosY)
                Case "."c
                    score += 10
                    If Not fruitTimer.IsRunning Then fruitTimer.Start()
                Case "*"c
                    isPowerPelletEaten = True
                    score += 50
                    With scaredTimer
                        Call If(.IsRunning, Sub() .Restart(), Sub() .Start())
                    End With
                Case "%"c
                    score += 100 + level * 200
                    fruitTimer.StopAndReset()
            End Select

            gameMap(playerPos.x, playerPos.y) = " "c
        End If

        ' This part of code displays the game map (i.e. the play area) on the screen.
        For y As Integer = 0 To UBound(gameMap, 2) Step 1
            For x As Integer = 0 To UBound(gameMap, 1) Step 1
                Select Case gameMap(x, y)
                    Case "#"c
                        Console.ForegroundColor = ConsoleColor.Green
                    Case "%"c
                        Console.ForegroundColor = ConsoleColor.DarkRed
                    Case "*"c, "."c
                        Console.ForegroundColor = ConsoleColor.White
                End Select
                Console.Write(gameMap(x, y))
            Next x
            Console.WriteLine()
        Next y

        Dim isCaughtByGhost As Boolean = False
        For Each enemy As EnemyInfo In enemyInfo
            Console.ForegroundColor = If(enemy.Scared, ConsoleColor.Blue, enemy.Color)
            Console.SetCursorPosition(enemy.CurrPos.x, enemy.CurrPos.y)
            Console.Write("&"c)
            If Not enemy.Scared AndAlso enemy.CurrPos = playerPos Then isCaughtByGhost = True
        Next enemy
        If isCaughtByGhost Then
            lives -= 1
            fruitTimer.StopAndReset()
            scaredTimer.StopAndReset()
            isPowerPelletEaten = False
            gameMap(13, 10) = " "c
            playerPos = New GridIndex(17, 10)
            For i As Integer = 0 To UBound(enemyInfo)
                enemyInfo(i).CurrPos = InitialEnemyInfo(i).InitialPos
                enemyInfo(i).Scared = False
            Next
        End If

        With scaredTimer
            If .IsRunning Then
                Dim millisecLeft# = TimerThreshold(level, True) - .ElapsedMilliseconds

                For i As Integer = 0 To UBound(enemyInfo) Step 1
                    If playerPos = enemyInfo(i).CurrPos Then
                        enemyInfo(i).CurrPos = InitialEnemyInfo(i).CurrPos
                        enemyInfo(i).Scared = False
                        bonusMult += 1
                        score += CInt(2 ^ bonusMult * 100)
                    End If
                    If millisecLeft <= 0 Then
                        bonusMult = 0
                        .StopAndReset()
                        Exit For
                    End If
                Next i
                If millisecLeft > 0 Then
                    Console.ForegroundColor = ConsoleColor.Yellow
                    Console.SetCursorPosition(25, 5)
                    Console.Write($"POWERUP: {Math.Floor(millisecLeft / 1000)} sec. left")
                End If
            End If
        End With

        With fruitTimer
            If .IsRunning AndAlso .ElapsedMilliseconds > TimerThreshold(level, False) Then
                gameMap(13, 10) = "%"c
                Dim interval As Integer = 15000 - (level - 1) * 2000
                If .ElapsedMilliseconds > TimerThreshold(level, False) + interval Then
                    gameMap(13, 10) = " "c
                    .StopAndReset()
                End If
            End If
        End With

        Dim hasPellet As Boolean = False
        For x As Integer = 0 To UBound(gameMap, 1) Step 1
            For y As Integer = 0 To UBound(gameMap, 2) Step 1
                If gameMap(x, y) = "."c OrElse gameMap(x, y) = "*"c Then hasPellet = True
            Next y
        Next x
        If Not hasPellet Then
            level += 1
            gameMap = CType(InitialGameMap.Clone(), Char(,))
            playerPos = New GridIndex(17, 10)
            Array.Copy(InitialEnemyInfo, enemyInfo, InitialEnemyInfo.Length)
            fruitTimer.StopAndReset()
            scaredTimer.StopAndReset()
        End If

        ' When the player loses all the lives, or completes all 7 levels, the game ends.
        ' The gameplay result will be displayed before going back to the title screen. 
        If lives <= 0 OrElse level > 7 Then
            gameMap = CType(InitialGameMap.Clone(), Char(,))
            fruitTimer.StopAndReset()
            scaredTimer.StopAndReset()
            isGameInProcess = False
            Console.Clear()
            Console.ForegroundColor = ConsoleColor.Yellow
            Console.SetCursorPosition(5, 10)
            Dim prevLvlInfo As String = If(level < 7, $"Level {level}", "All Clear!")
            Console.Write($"Previous Score: {score,5} ({prevLvlInfo})")
            score = 0 : lives = 3 : level = 1
            Exit Sub
        End If

        If Not playerHasExtraLife AndAlso score > 10000 Then
            lives += 1
            playerHasExtraLife = True
        End If

        Console.ForegroundColor = ConsoleColor.Yellow
        Console.SetCursorPosition(playerPos.x, playerPos.y)
        Console.Write("@"c)
        Console.SetCursorPosition(25, 0)
        Console.Write($"Score: {score,5}")
        Console.SetCursorPosition(25, 1)
        Console.Write($"Lives: {New String("@"c, lives - 1)}")

        Console.ForegroundColor = ConsoleColor.DarkRed
        Console.SetCursorPosition(25, 3)
        Console.Write(New String("%"c, level))

        Threading.Thread.Sleep(500 - level * 50)
    End Sub

    Private ReadOnly Property PlayerDirection As GridIndex
        Get
            Dim keyMapping As New Dictionary(Of ConsoleKey, GridIndex) From {
                {ConsoleKey.UpArrow, GridIndex.Up},
                {ConsoleKey.DownArrow, GridIndex.Down},
                {ConsoleKey.LeftArrow, GridIndex.Left},
                {ConsoleKey.RightArrow, GridIndex.Right}
            }
            Return keyMapping.GetValueOrDefault(pressedKey.Key)
        End Get
    End Property

    Private ReadOnly Property MaxLineIndex(lines As String()) As Integer
        Get
            Dim longestLine =
                Aggregate line As String In lines Order By line.Length Descending Into First()

            Return UBound(longestLine.ToCharArray())
        End Get
    End Property

    Private Function GetRandomDirection(currentPos As GridIndex) As GridIndex
        Dim directions As New List(Of GridIndex) From {
            GridIndex.Up, GridIndex.Down, GridIndex.Left, GridIndex.Right
        }
        Dim validDirections As New List(Of GridIndex)

        For Each dir As GridIndex In directions
            Dim nextPos = currentPos + dir
            If IsWalkable(nextPos) Then validDirections.Add(dir)
        Next dir

        If validDirections.Count > 0 Then
            Return validDirections(CInt(Rnd() * (validDirections.Count - 1)))
        End If

        Return GridIndex.Up
    End Function

    Private Function IsWalkable(pos As GridIndex) As Boolean
        Return pos.x >= 0 AndAlso pos.x <= UBound(WalkableTerrain, 1) AndAlso
               pos.y >= 0 AndAlso pos.y <= UBound(WalkableTerrain, 2) AndAlso
               WalkableTerrain(pos.x, pos.y)
    End Function
End Module

Friend Structure GridIndex
    Public ReadOnly x As Integer, y As Integer

    Public Sub New(x As Integer, y As Integer)
        Me.x = x
        Me.y = y
    End Sub

    Public Overrides Function Equals(<NotNullWhen(True)> obj As Object) As Boolean
        Dim other As GridIndex = DirectCast(obj, GridIndex)
        Return x = other.x AndAlso y = other.y
    End Function

    Public Shared Operator =(left As GridIndex, right As GridIndex) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As GridIndex, right As GridIndex) As Boolean
        Return Not left.Equals(right)
    End Operator

    Public Shared Operator +(left As GridIndex, right As GridIndex) As GridIndex
        Return New GridIndex(left.x + right.x, left.y + right.y)
    End Operator

    Public Shared Operator -(left As GridIndex, right As GridIndex) As GridIndex
        Return New GridIndex(left.x - right.x, left.y - right.y)
    End Operator

    Public Shared Operator *(vec As GridIndex, scale As Integer) As GridIndex
        Return New GridIndex(vec.x * scale, vec.y * scale)
    End Operator

    Public Shared ReadOnly Property Up As New GridIndex(0, -1)
    Public Shared ReadOnly Property Down As New GridIndex(0, 1)
    Public Shared ReadOnly Property Left As New GridIndex(-1, 0)
    Public Shared ReadOnly Property Right As New GridIndex(1, 0)

    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(x, y)
    End Function
End Structure

Friend NotInheritable Class AStarAlgorithm
    Private ReadOnly gridIdx As GridIndex

    Public Overrides Function Equals(obj As Object) As Boolean
        Dim other As AStarAlgorithm = TryCast(obj, AStarAlgorithm)
        Return other IsNot Nothing AndAlso gridIdx = other.gridIdx
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return gridIdx.GetHashCode()
    End Function

    Public Shared Function FindUniqueRoute(walkableTerrain As Boolean(,),
             startGridIdx As GridIndex, finishGridIdx As GridIndex) As List(Of GridIndex)

        If startGridIdx = finishGridIdx Then
            Return New List(Of GridIndex)()
        End If

        If finishGridIdx.x < 0 OrElse finishGridIdx.x > UBound(walkableTerrain, 1) OrElse
           finishGridIdx.y < 0 OrElse finishGridIdx.y > UBound(walkableTerrain, 2) OrElse
           Not walkableTerrain(finishGridIdx.x, finishGridIdx.y) Then
            Return New List(Of GridIndex)()
        End If

        Dim openList As New PriorityQueue(Of GridIndex, Integer)()
        openList.Enqueue(startGridIdx, 0)

        Dim gScore As New Dictionary(Of GridIndex, Integer)()
        gScore(startGridIdx) = 0

        Dim cameFrom As New Dictionary(Of GridIndex, GridIndex)()

        Dim Heuristic = Function(a As GridIndex, b As GridIndex) As Integer
                            Return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y)
                        End Function

        Dim directions As GridIndex() = {GridIndex.Up, GridIndex.Down, GridIndex.Left, GridIndex.Right}

        While openList.Count > 0
            Dim currPoint = openList.Dequeue()

            If currPoint = finishGridIdx Then
                Dim path As New List(Of GridIndex)
                Dim current = currPoint

                While cameFrom.ContainsKey(current)
                    path.Add(current)
                    current = cameFrom(current)
                End While

                path.Reverse()

                Dim route As New List(Of GridIndex)
                For i As Integer = 1 To path.Count - 1
                    Dim direction = path(i) - path(i - 1)
                    route.Add(direction)
                Next

                Return route
            End If

            For Each direction In directions
                Dim nextGridIdx = currPoint + direction

                If nextGridIdx.x < 0 OrElse nextGridIdx.x > UBound(walkableTerrain, 1) OrElse
                   nextGridIdx.y < 0 OrElse nextGridIdx.y > UBound(walkableTerrain, 2) Then
                    Continue For
                End If

                If Not walkableTerrain(nextGridIdx.x, nextGridIdx.y) Then
                    Continue For
                End If

                Dim tentativeG = gScore(currPoint) + 1
                Dim existingG As Integer
                If Not gScore.TryGetValue(nextGridIdx, existingG) OrElse tentativeG < existingG Then
                    cameFrom(nextGridIdx) = currPoint
                    gScore(nextGridIdx) = tentativeG
                    Dim fScore = tentativeG + Heuristic(nextGridIdx, finishGridIdx)
                    openList.Enqueue(nextGridIdx, fScore)
                End If
            Next
        End While

        Return New List(Of GridIndex)
    End Function
End Class