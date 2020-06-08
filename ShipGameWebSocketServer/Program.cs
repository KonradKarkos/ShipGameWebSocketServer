using SuperSocket.Common;
using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            if (gameSessions.Where(g => g.PlayerOneSessionID.Equals(session.SessionID)).Any())
            {
                var games = gameSessions.Where(g => g.PlayerOneSessionID.Equals(session.SessionID)).ToList();
                foreach (GameSession g in games)
                {
                    gameSessions[gameSessions.IndexOf(g)].PlayerOneReady = false;
                    gameSessions[gameSessions.IndexOf(g)].PlayerOneSessionID = "";
                    if (g.GameState != GameState.Lost && g.GameState != GameState.Won)
                    {
                        gameSessions[gameSessions.IndexOf(g)].GameState = GameState.Waiting;
                    }
                    if (wsServer.GetAllSessions().Where(s => s.SessionID.Equals(g.PlayerTwoSessionID)).Any())
                    {
                        msg = Encoding.UTF8.GetBytes("Other player disconnected");
                        wsServer.GetAppSessionByID(g.PlayerTwoSessionID).Send(msg, 0, msg.Length);
                    }
                }
            }
            if (gameSessions.Where(g => g.PlayerTwoSessionID.Equals(session.SessionID)).Any())
            {
                var games = gameSessions.Where(g => g.PlayerTwoSessionID.Equals(session.SessionID)).ToList();
                foreach (GameSession g in games)
                {
                    gameSessions[gameSessions.IndexOf(g)].PlayerTwoReady = false;
                    gameSessions[gameSessions.IndexOf(g)].PlayerTwoSessionID = "";
                    if (g.GameState != GameState.Lost && g.GameState != GameState.Won)
                    {
                        gameSessions[gameSessions.IndexOf(g)].GameState = GameState.Waiting;
                    }
                    if (wsServer.GetAllSessions().Where(s => s.SessionID.Equals(g.PlayerOneSessionID)).Any())
                    {
                        msg = Encoding.UTF8.GetBytes("Other player disconnected");
                        wsServer.GetAppSessionByID(g.PlayerOneSessionID).Send(msg, 0, msg.Length);
                    }
                }
            }
            Console.WriteLine(session.SessionID + " closed session");
        }

        private static void WsServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            byte[] msg;
            int gameID;
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
                            gameSessions.Add(new GameSession(session.SessionID));
                            gameID = gameSessions.IndexOf(gameSessions.Find(g => g.PlayerOneSessionID.Equals(session.SessionID)));
                            msg = Encoding.UTF8.GetBytes("New game created with ID " + gameID);
                            session.Send(msg, 0, msg.Length);
                        }
                    }
                    else
                    {
                        gameSessions.Add(new GameSession(session.SessionID));
                        gameID = gameSessions.IndexOf(gameSessions.Find(g => g.PlayerOneSessionID.Equals(session.SessionID)));
                        msg = Encoding.UTF8.GetBytes("New game created with ID " + gameID);
                        session.Send(msg, 0, msg.Length);
                    }
                    break;
                case MessageType.JoinThisGame:
                    if (gameSessions.Count >= (int)value[1])
                    {
                        if (gameSessions[(int)value[1]].TryAddPlayer(session.SessionID))
                        {
                            session.Send(value, 0, value.Length);
                            msg = new byte[201];
                            msg[0] = (byte)MessageType.Boards;
                            if(gameSessions[(int)value[1]].PlayerOneSessionID.Equals(session.SessionID))
                            {
                                PrepareBoardMessage(msg, gameSessions[(int)value[1]].Boards, 0, 1);
                            }
                            else
                            {
                                PrepareBoardMessage(msg, gameSessions[(int)value[1]].Boards, 1, 1);
                            }
                            session.Send(msg, 0, msg.Length);
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
                    if (gameSessions.Count > 0 && gameSessions.Where(g => g.PlayerOneSessionID.Length == 0 || g.PlayerTwoSessionID.Length == 0).Any())
                    {
                        List<GameSession> openSessions = gameSessions.Where(g => g.PlayerOneSessionID.Length == 0 || g.PlayerTwoSessionID.Length == 0).ToList();
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
                                if (openSessions[i].PlayerOneSessionID.Equals(session.SessionID))
                                {
                                    PrepareBoardMessage(msg, openSessions[i].Boards, 0, 1);
                                }
                                else
                                {
                                    PrepareBoardMessage(msg, openSessions[i].Boards, 1, 1);
                                }
                                session.Send(msg, 0, msg.Length);
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
                    var gamesR = gameSessions.Where(g => g.PlayerOneSessionID.Equals(session.SessionID) || g.PlayerTwoSessionID.Equals(session.SessionID));
                    if (gamesR.Any())
                    {
                        gameID = gameSessions.IndexOf(gamesR.First());
                        if (gameSessions[gameID].GameState == GameState.Waiting)
                        {
                            msg = new byte[201];
                            msg[0] = (byte)MessageType.Boards;
                            if (gameSessions[gameID].PlayerOneSessionID.Equals(session.SessionID))
                            {
                                RandShipsBoard(gameSessions[gameID].PlayerOneShips).CopyTo(gameSessions[gameID].Boards, 0);
                                PrepareBoardMessage(msg, gameSessions[gameID].Boards, 0, 1);
                                session.Send(msg, 0, msg.Length);
                                if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerTwoSessionID)).Any())
                                {
                                    PrepareBoardMessage(msg, gameSessions[gameID].Boards, 1, 1);
                                    wsServer.GetAppSessionByID(gameSessions[gameID].PlayerTwoSessionID).Send(msg, 0, msg.Length);
                                }
                            }
                            else
                            {
                                RandShipsBoard(gameSessions[gameID].PlayerTwoShips).CopyTo(gameSessions[gameID].Boards, 100);
                                PrepareBoardMessage(msg, gameSessions[gameID].Boards, 1, 1);
                                session.Send(msg, 0, msg.Length);
                                if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerOneSessionID)).Any())
                                {
                                    PrepareBoardMessage(msg, gameSessions[gameID].Boards, 0, 1);
                                    wsServer.GetAppSessionByID(gameSessions[gameID].PlayerOneSessionID).Send(msg, 0, msg.Length);
                                }
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
                        var gamesA = gameSessions.Where(g => (g.PlayerOneSessionID.Equals(session.SessionID) || g.PlayerTwoSessionID.Equals(session.SessionID)) && g.GameState == GameState.InProgress);
                        if (gamesA.Any())
                        {
                            gameID = gameSessions.IndexOf(gamesA.First());
                            if (gameSessions[gameID].PlayerOneTurn && gameSessions[gameID].PlayerOneSessionID.Equals(session.SessionID))
                            {
                                if (gameSessions[gameID].AttackCoordinates(1, letter, number))
                                {
                                    msg = new byte[201];
                                    msg[0] = (byte)MessageType.Boards;
                                    PrepareBoardMessage(msg, gameSessions[gameID].Boards, 0, 1);
                                    gameSessions[gameID].PlayerOneTurn = false;
                                    session.Send(msg, 0, msg.Length);
                                    GameState gameState = gameSessions[gameID].GetRefreshedGameState();
                                    if (gameState == GameState.Lost)
                                    {
                                        var msg2 = Encoding.UTF8.GetBytes("You lost!");
                                        session.Send(msg2, 0, msg2.Length);
                                    }
                                    else if (gameState == GameState.Won)
                                    {
                                        var msg2 = Encoding.UTF8.GetBytes("You won!");
                                        session.Send(msg2, 0, msg2.Length);
                                    }
                                    if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerTwoSessionID)).Any())
                                    {
                                        PrepareBoardMessage(msg, gameSessions[gameID].Boards, 1, 1);
                                        wsServer.GetAppSessionByID(gameSessions[gameID].PlayerTwoSessionID).Send(msg, 0, msg.Length);
                                        if (gameState == GameState.Lost)
                                        {
                                            var msg2 = Encoding.UTF8.GetBytes("You won!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerTwoSessionID).Send(msg2, 0, msg2.Length);
                                        }
                                        else if (gameState == GameState.Won)
                                        {
                                            var msg2 = Encoding.UTF8.GetBytes("You lost!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerTwoSessionID).Send(msg2, 0, msg2.Length);
                                        }
                                    }
                                }
                                else
                                {
                                    msg = Encoding.UTF8.GetBytes("Cannot attack those coordinates");
                                    session.Send(msg, 0, msg.Length);
                                }
                            }
                            else if (!gameSessions[gameID].PlayerOneTurn && gameSessions[gameID].PlayerTwoSessionID.Equals(session.SessionID))
                            {
                                if (gameSessions[gameID].AttackCoordinates(0, letter, number))
                                {
                                    msg = new byte[201];
                                    msg[0] = (byte)MessageType.Boards;
                                    gameSessions[gameID].PlayerOneTurn = true;
                                    PrepareBoardMessage(msg, gameSessions[gameID].Boards, 1, 1);
                                    GameState gameState = gameSessions[gameID].GetRefreshedGameState();
                                    session.Send(msg, 0, msg.Length);
                                    if (gameState == GameState.Lost)
                                    {
                                        var msg2 = Encoding.UTF8.GetBytes("You won!");
                                        session.Send(msg2, 0, msg2.Length);
                                    }
                                    else if (gameState == GameState.Won)
                                    {
                                        var msg2 = Encoding.UTF8.GetBytes("You lost!");
                                        session.Send(msg2, 0, msg2.Length);
                                    }
                                    if (wsServer.GetSessions(s => s.SessionID.Equals(gameSessions[gameID].PlayerOneSessionID)).Any())
                                    {
                                        PrepareBoardMessage(msg, gameSessions[gameID].Boards, 0, 1);
                                        wsServer.GetAppSessionByID(gameSessions[gameID].PlayerOneSessionID).Send(msg, 0, msg.Length);
                                        if (gameState == GameState.Lost)
                                        {
                                            var msg2 = Encoding.UTF8.GetBytes("You lost!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerOneSessionID).Send(msg2, 0, msg2.Length);
                                        }
                                        else if (gameState == GameState.Won)
                                        {
                                            var msg2 = Encoding.UTF8.GetBytes("You won!");
                                            wsServer.GetAppSessionByID(gameSessions[gameID].PlayerOneSessionID).Send(msg2, 0, msg2.Length);
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
                    if (gameSessions.Where(g => g.PlayerOneSessionID.Equals(session.SessionID)).Any())
                    {
                        var games = gameSessions.Where(g => g.PlayerOneSessionID.Equals(session.SessionID)).ToList();
                        foreach (GameSession g in games)
                        {
                            gameSessions[gameSessions.IndexOf(g)].PlayerOneReady = true;
                            msg = Encoding.UTF8.GetBytes("Other player ready");
                            if (g.PlayerOneReady && g.PlayerTwoReady)
                            {
                                gameSessions[gameSessions.IndexOf(g)].GameState = GameState.InProgress;
                            }
                            wsServer.GetAppSessionByID(g.PlayerTwoSessionID).Send(msg, 0, msg.Length);
                        }
                    }
                    if (gameSessions.Where(g => g.PlayerTwoSessionID.Equals(session.SessionID)).Any())
                    {
                        var games = gameSessions.Where(g => g.PlayerTwoSessionID.Equals(session.SessionID)).ToList();
                        foreach (GameSession g in games)
                        {
                            gameSessions[gameSessions.IndexOf(g)].PlayerTwoReady = true;
                            msg = Encoding.UTF8.GetBytes("Other player ready");
                            if (g.PlayerOneReady && g.PlayerTwoReady)
                            {
                                gameSessions[gameSessions.IndexOf(g)].GameState = GameState.InProgress;
                            }
                            wsServer.GetAppSessionByID(g.PlayerOneSessionID).Send(msg, 0, msg.Length);
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
