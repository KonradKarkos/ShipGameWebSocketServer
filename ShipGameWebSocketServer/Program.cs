using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShipGameWebSocketServer
{
    public enum MessageType
    {
        CreateGame,
        JoinThisGame,
        JoinGame,
        RandShips,
        Ready,
        Attack,
        GameState,
        Boards
    }
    class Program
    {
        private static WebSocketServer wsServer;
        private static volatile List<GameSession> gameSessions;
        private static int boardSize = 100;
        static void Main(string[] args)
        {
            wsServer = new WebSocketServer();
            int port = 8888;
            gameSessions = new List<GameSession>();
            wsServer.Setup(port);
            wsServer.NewSessionConnected += WsServer_NewSessionConnected;
            wsServer.NewMessageReceived += WsServer_NewMessageReceived;
            wsServer.NewDataReceived += WsServer_NewDataReceived;
            wsServer.SessionClosed += WsServer_SessionClosed;
            wsServer.Start();
            Console.WriteLine("Server is running on port " + port + ". Press ENTER to exit....");
            Console.ReadKey();
        }

        private static void WsServer_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
            byte[] msg;
            if (gameSessions.Where(g => g.PlayerSessionIDs.Contains(session.SessionID)).Any())
            {
                var games = gameSessions.Where(g => g.PlayerSessionIDs.Contains(session.SessionID)).ToList();
                int gameIndex;
                int playerIndex;
                int opponentIndex;
                foreach (GameSession g in games)
                {
                    gameIndex = gameSessions.IndexOf(g);
                    playerIndex = Array.IndexOf(gameSessions[gameIndex].PlayerSessionIDs, session.SessionID);
                    if (playerIndex == 0) opponentIndex = 1;
                    else opponentIndex = 0;
                    gameSessions[gameIndex].PlayersReady[playerIndex] = false;
                    gameSessions[gameSessions.IndexOf(g)].PlayerSessionIDs[playerIndex] = "";
                    if (g.GameState != GameState.Lost && g.GameState != GameState.Won)
                    {
                        gameSessions[gameSessions.IndexOf(g)].GameState = GameState.Interrupted;
                    }
                    if (wsServer.GetAllSessions().Where(s => s.SessionID.Equals(g.PlayerSessionIDs[opponentIndex])).Any())
                    {
                        msg = Encoding.UTF8.GetBytes("Other player disconnected");
                        wsServer.GetAppSessionByID(g.PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                    }
                }
            }
            Console.WriteLine(session.SessionID + " closed session");
        }

        private static void WsServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            byte[] msg;
            int gameID;
            int playerIndex;
            int opponentIndex;
            switch ((MessageType)value[0])
            {
                case MessageType.CreateGame:
                    if (gameSessions.Count >= 256)
                    {
                        if (gameSessions.Where(g => g.GameState != GameState.Won && g.GameState != GameState.Lost).Any())
                        {
                            msg = Encoding.UTF8.GetBytes("Too many games, please wait for resolvment of the rest of games.");
                            session.Send(msg, 0, msg.Length);
                        }
                        else
                        {
                            gameSessions.Clear();
                            GameSession gameSession = new GameSession(session.SessionID);
                            gameSessions.Add(gameSession);
                            gameID = gameSessions.IndexOf(gameSession);
                            msg = Encoding.UTF8.GetBytes("New game created with ID " + gameID);
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        GameSession gameSession = new GameSession(session.SessionID);
                        gameSessions.Add(gameSession);
                        gameID = gameSessions.IndexOf(gameSession);
                        msg = Encoding.UTF8.GetBytes("New game created with ID " + gameID);
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.JoinThisGame:
                    gameID = (int)value[1];
                    if (gameSessions.Count >= gameID)
                    {
                        if (gameSessions[gameID].TryAddPlayer(session.SessionID))
                        {
                            session.Send(value, 0, value.Length);
                            msg = new byte[201];
                            msg[0] = (byte)MessageType.Boards;
                            playerIndex = Array.IndexOf(gameSessions[gameID].PlayerSessionIDs, session.SessionID);
                            if (playerIndex == 0) opponentIndex = 1;
                            else opponentIndex = 0;
                            PrepareBoardMessage(msg, gameSessions[gameID].Boards, playerIndex, 1);
                            session.Send(msg, 0, msg.Length);
                            if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerSessionIDs[opponentIndex])).Any())
                            {
                                msg = Encoding.UTF8.GetBytes("Another player joined the game!");
                                wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                            }
                        }
                        else
                        {
                            msg = new byte[2];
                            msg[0] = (byte)MessageType.GameState;
                            msg[1] = (byte)gameSessions[(int)value[1]].GameState;
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        msg = Encoding.UTF8.GetBytes("Game not found");
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.JoinGame:
                    if (gameSessions.Count > 0 && gameSessions.Where(g => g.PlayerSessionIDs.Where(s => s.Length == 0).Any()).Any())
                    {
                        List<GameSession> openSessions = gameSessions.Where(g => g.PlayerSessionIDs.Where(s => s.Length == 0).Any()).ToList();
                        bool sent = false;
                        for (int i = 0; i < openSessions.Count; i++)
                        {
                            gameID = gameSessions.IndexOf(openSessions[i]);
                            if (openSessions[i].TryAddPlayer(session.SessionID))
                            {
                                msg = new byte[2];
                                msg[0] = (byte)MessageType.JoinGame;
                                msg[1] = (byte)gameID;
                                session.Send(msg, 0, msg.Length);
                                msg = new byte[201];
                                msg[0] = (byte)MessageType.Boards;
                                playerIndex = Array.IndexOf(openSessions[i].PlayerSessionIDs, session.SessionID);
                                if (playerIndex == 0) opponentIndex = 1;
                                else opponentIndex = 0;
                                PrepareBoardMessage(msg, openSessions[i].Boards, playerIndex, 1);
                                session.Send(msg, 0, msg.Length);
                                if (wsServer.GetSessions(s => s.SessionID.Equals(openSessions[i].PlayerSessionIDs[opponentIndex])).Any())
                                {
                                    msg = Encoding.UTF8.GetBytes("Another player joined the game!");
                                    wsServer.GetAppSessionByID(openSessions[i].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                                }
                                sent = true;
                                break;
                            }
                        }
                        if (!sent)
                        {
                            msg = Encoding.UTF8.GetBytes("Game not found");
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        msg = Encoding.UTF8.GetBytes("Game not found");
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.RandShips:
                    var gamesR = gameSessions.Where(g => g.PlayerSessionIDs.Contains(session.SessionID));
                    if (gamesR.Any())
                    {
                        gameID = gameSessions.IndexOf(gamesR.First());
                        if (gameSessions[gameID].GameState == GameState.Waiting)
                        {
                            msg = new byte[201];
                            msg[0] = (byte)MessageType.Boards;
                            playerIndex = Array.IndexOf(gameSessions[gameID].PlayerSessionIDs, session.SessionID);
                            if (playerIndex == 0) opponentIndex = 1;
                            else opponentIndex = 0;
                            RandShipsBoard(gameSessions[gameID].PlayersShips[playerIndex]).CopyTo(gameSessions[gameID].Boards, 100 * playerIndex);
                            PrepareBoardMessage(msg, gameSessions[gameID].Boards, playerIndex, 1);
                            session.Send(msg, 0, msg.Length);
                            if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerSessionIDs[opponentIndex])).Any())
                            {
                                PrepareBoardMessage(msg, gameSessions[gameID].Boards, opponentIndex, 1);
                                wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                            }
                        }
                        else
                        {
                            msg = Encoding.UTF8.GetBytes("Cannot change ship placement to already started game");
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        msg = Encoding.UTF8.GetBytes("Game not found");
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.Attack:
                    int letter = (int)value[1];
                    int number = (int)value[2];
                    if (letter >= 0 && letter <= 9 && number >= 0 && number <= 9)
                    {
                        var gamesA = gameSessions.Where(g => (g.PlayerSessionIDs.Contains(session.SessionID)) && g.GameState == GameState.InProgress);
                        if (gamesA.Any())
                        {
                            gameID = gameSessions.IndexOf(gamesA.First());
                            playerIndex = Array.IndexOf(gameSessions[gameID].PlayerSessionIDs, session.SessionID);
                            if (playerIndex == 0) opponentIndex = 1;
                            else opponentIndex = 0;
                            if (gameSessions[gameID].PlayerTurns[playerIndex])
                            {
                                if (gameSessions[gameID].AttackCoordinates(opponentIndex, letter, number))
                                {
                                    msg = new byte[201];
                                    msg[0] = (byte)MessageType.Boards;
                                    PrepareBoardMessage(msg, gameSessions[gameID].Boards, playerIndex, 1);
                                    gameSessions[gameID].PlayerTurns[playerIndex] = false;
                                    gameSessions[gameID].PlayerTurns[opponentIndex] = true;
                                    session.Send(msg, 0, msg.Length);
                                    GameState gameState = gameSessions[gameID].GetRefreshedGameState();
                                    if (gameState == GameState.Lost)
                                    {
                                        msg = Encoding.UTF8.GetBytes("You lost!");
                                        session.Send(msg, 0, msg.Length);
                                    }
                                    else if (gameState == GameState.Won)
                                    {
                                        msg = Encoding.UTF8.GetBytes("You won!");
                                        session.Send(msg, 0, msg.Length);
                                    }
                                    if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerSessionIDs[opponentIndex])).Any())
                                    {
                                        msg = new byte[201];
                                        msg[0] = (byte)MessageType.Boards;
                                        PrepareBoardMessage(msg, gameSessions[gameID].Boards, opponentIndex, 1);
                                        wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                                        if (gameState == GameState.Lost)
                                        {
                                            msg = Encoding.UTF8.GetBytes("You won!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                                        }
                                        else if (gameState == GameState.Won)
                                        {
                                            msg = Encoding.UTF8.GetBytes("You lost!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                                        }
                                        else if (gameState == GameState.InProgress)
                                        {
                                            msg = new byte[1];
                                            msg[0] = (byte)MessageType.Ready;
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                                        }
                                    }
                                }
                                else
                                {
                                    msg = Encoding.UTF8.GetBytes("Cannot attack those coordinates");
                                    session.Send(msg, 0, msg.Length);
                                }
                            }
                            else
                            {
                                msg = Encoding.UTF8.GetBytes("Not your turn!");
                                session.Send(msg, 0, msg.Length);
                            }
                        }
                        else
                        {
                            msg = Encoding.UTF8.GetBytes("Could not find game in progress");
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        msg = Encoding.UTF8.GetBytes("Wrong command");
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.Ready:
                    if (gameSessions.Where(g => g.PlayerSessionIDs.Contains(session.SessionID)).Any())
                    {
                        gameID = gameSessions.IndexOf(gameSessions.Where(g => g.PlayerSessionIDs.Contains(session.SessionID)).First());
                        playerIndex = Array.IndexOf(gameSessions[gameID].PlayerSessionIDs, session.SessionID);
                        if (playerIndex == 0) opponentIndex = 1;
                        else opponentIndex = 0;
                        gameSessions[gameID].PlayersReady[playerIndex] = true;
                        if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerSessionIDs[opponentIndex])).Any())
                        {
                            msg = Encoding.UTF8.GetBytes("Other player ready");
                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[opponentIndex]).Send(msg, 0, msg.Length);
                        }
                        else
                        {
                            msg = Encoding.UTF8.GetBytes("Ready acknowleged, waiting for another player to join game.");
                            session.Send(msg, 0, msg.Length);
                        }
                        if (!gameSessions[gameID].PlayersReady.Contains(false))
                        {
                            gameSessions[gameID].GameState = GameState.InProgress;
                            for (int i = 0; i < gameSessions[gameID].PlayerTurns.Length; i++)
                            {
                                if (gameSessions[gameID].PlayerTurns[i])
                                {
                                    msg = new byte[1];
                                    msg[0] = (byte)MessageType.Ready;
                                    wsServer.GetAppSessionByID(gameSessions[gameID].PlayerSessionIDs[i]).Send(msg, 0, msg.Length);
                                    break;
                                }
                            }
                        }

                    }
                    break;
                default:
                    msg = Encoding.UTF8.GetBytes("Unknown command");
                    session.Send(msg, 0, msg.Length);
                    break;
            }
            Console.WriteLine("NewDataReceived: " + ((MessageType)value[0]).ToString() + " from " + session.SessionID);
        }

        private static void WsServer_NewMessageReceived(WebSocketSession session, string value)
        {
            Console.WriteLine("NewMessageReceived: " + value + " from " + session.SessionID);
            if (value == "Hello server")
            {
                session.Send("Hello client");
            }
        }

        private static void WsServer_NewSessionConnected(WebSocketSession session)
        {
            Console.WriteLine("NewSessionConnected");
        }
        private static void PrepareBoardMessage(byte[] msg, byte[] boards, int targetPlayer, int offset)
        {
            if(targetPlayer == 0)
            {
                new ArraySegment<byte>(boards, 0, 100).ToArray().CopyTo(msg, offset);
                CoverIntactShips(new ArraySegment<byte>(boards, 100, 100).ToArray()).CopyTo(msg, 100 + offset);
            }
            else
            {
                new ArraySegment<byte>(boards, 100, 100).ToArray().CopyTo(msg, offset);
                CoverIntactShips(new ArraySegment<byte>(boards, 0, 100).ToArray()).CopyTo(msg, 100 + offset);
            }
        }
        private static byte[] CoverIntactShips(byte[] board)
        {
            for(int i=0; i< board.Length; i++)
            {
                if((CoordinateState)board[i] == CoordinateState.Ship)
                {
                    board[i] = (byte)CoordinateState.Water;
                }
            }
            return board;
        }
        private static void PlaceShip(byte[] Board, int size, Dictionary<int, KeyValuePair<int, int>> PlayerShips)
        {
            Random random = new Random();
            bool placed = false;
            int freeBlocks;
            int direction;
            int position;
            int multiplier;
            while(!placed)
            {
                direction = random.Next(0, 3);
                position = random.Next(0, boardSize - 1);
                freeBlocks = 0;
                switch(direction)
                {
                    //down
                    case 0:
                        multiplier = 10;
                        break;
                    //left
                    case 1:
                        multiplier = -1;
                        break;
                    //up
                    case 2:
                        multiplier = -10;
                        break;
                    //right
                    case 3:
                        multiplier = 1;
                        break;
                    default:
                        multiplier = 1;
                        break;
                }
                for (int i = 0; i < size; i++)
                {
                    if (Board.Length > (position + i * multiplier) && (position + i * multiplier) > 0 && (CoordinateState)Board[position + i * multiplier] == CoordinateState.Water)
                    {
                        freeBlocks++;
                    }
                }
                if (freeBlocks == size)
                {
                    for (int i = 0; i < size; i++)
                    {
                        Board[position + i * multiplier] = (byte)CoordinateState.Ship;
                    }
                    PlayerShips.Add(size, new KeyValuePair<int, int>(multiplier, position));
                    placed = true;
                }
            }
        }
        private static byte[] RandShipsBoard(Dictionary<int, KeyValuePair<int, int>> PlayerShips)
        {
            PlayerShips.Clear();
            byte[] Board = new byte[boardSize];
            for (int i=0;i< boardSize; i++)
            {
                Board[i] = (byte)CoordinateState.Water;
            }
            for(int i=1;i<6;i++)
            {
                PlaceShip(Board, i, PlayerShips);
            }
            return Board;
        }
    }
}
