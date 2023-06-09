using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Numerics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using Nager.DataFragmentationHandler;
using ReactiveSockets;

namespace GPN23
{
    class Program
    {
        static void Main(string[] args)
        {
            Game g = new Game();
            g.Start();
        }
    }

    class Game
    {
        public TcpClient client;
        public const string host = "gpn-tron.duckdns.org";
        public int port = 4000;
        public int gameWidth = 0;
        public int gameHeight = 0;
        public int playerId = 0;
        public List<Player> players = new List<Player>();
        public string nextMove = "up";
        
        public string name = "ComputerElite";
        public string password = "fuckYou";
        //public string name = "ComputerElite";
        //public string password = "fuckYou";
        public int wins = 0;
        public int losses = 0;
        public int[,] board = new int[0,0];
        private bool isInGame = false;
        public void Connect()
        {
            Thread t = new Thread(() =>
            {
                bool connected = false;
                while (!connected)
                {
                    try
                    {
                        client = new TcpClient();
                        client.Connect(host, port);
                        StreamReader r = new StreamReader(client.GetStream());
                        connected = true;
                        while (true)
                        {
                            string l = r.ReadLine();
                    
                            ProcessMessage(l);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Couldn't connect to server: " + e + "... Trying again");
                        Thread.Sleep(1000);
                    }
                }
            });
            t.Start();
        }

        private void OnDataReceived(byte[] receivedData)
        {
            string s = Encoding.UTF8.GetString(receivedData);
            Console.WriteLine(s);
            ProcessMessage(s);
        }

        public Vector2 GetVector2InPlayfield(Vector2 i)
        {
            Vector2 inPlayField = new Vector2(i.X, i.Y);
            inPlayField.X %= gameWidth;
            inPlayField.Y %= gameHeight;
            if (inPlayField.X < 0) inPlayField.X = gameWidth + inPlayField.X;
            if (inPlayField.Y < 0) inPlayField.Y = gameHeight + inPlayField.Y;
            return inPlayField;
        }

        public bool IsPositionOccupied(Vector2 pos, int[,] board)
        {
            if (gameWidth == 0 || gameHeight == 0) return false;
            pos = GetVector2InPlayfield(pos);
            return board[(int)pos.Y, (int)pos.X] != -1;
        }

        public Vector2 GetOwnPosition()
        {
            Player self = players.FirstOrDefault(x => x.playerId == playerId);
            if (self == null) return new Vector2(0, 0);
            return self.positions.Last();
        }

        private void ProcessMessage(string s)
        {
            if (s == null) return;
            s = s.Replace("\n", "").Trim();
            // Print messages
            //Console.WriteLine(s);
            string[] args = s.Split('|');
            switch (args[0])
            {
                case "motd":
                    Console.WriteLine("The moto of the day is: " + args[1]);
                    Send("join|" + name + "|" + password);
                    break;
                case "error":
                    Console.WriteLine("Error: " + args[1]);
                    break;
                case "game":
                    isInGame = true;
                    gameWidth = ParseInt(args[1]);
                    gameHeight = ParseInt(args[2]);
                    playerId = ParseInt(args[3]);
                    board = new int[gameHeight, gameWidth];
                    for (int x = 0; x < gameHeight; x++)
                    {
                        for (int y = 0; y < gameWidth; y++)
                        {
                            board[y, x] = -1;
                        }
                    }
                    Console.WriteLine("game width: " + gameWidth);
                    Console.WriteLine("game height: " + gameHeight);
                    Console.WriteLine("player id: " + playerId);
                    players.Clear();
                    break;
                case "pos":
                    UpdatePlayerPosition(ParseInt(args[1]), ParseInt(args[2]), ParseInt(args[3]));
                    break;
                case "tick":
                    // Send package
                    if (!isInGame) return;
                    ShowBoard();
                    ComputeMove();
                    Send("move|" + nextMove);
                    break;
                case "die":
                    for (int i = 1; i < args.Length; i++)
                    {
                        players[GetPlayerIndexBasedOnId(ParseInt(args[i]))].dead = true;
                        CleanPlayerFromBoard(ParseInt(args[i]));
                    }
                    break;
                case "win":
                    isInGame = false;
                    wins = ParseInt(args[1]);
                    losses = ParseInt(args[2]);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Hehe get reckt! I won!!!! >:3");
                    Console.WriteLine("wins: " + wins.ToString().PadLeft(3) + "  -  losses: " + losses.ToString().PadLeft(3));
                    break;
                case "lose":
                    isInGame = false;
                    wins = ParseInt(args[1]);
                    losses = ParseInt(args[2]);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("I'm a looser D:");
                    break;
            }
        }

        public void CleanPlayerFromBoard(int playerId)
        {
            for (int y = 0; y < gameHeight; y++)
            {
                for (int x = 0; x < gameWidth; x++)
                {
                    int p = board[y, x];
                    if(p == playerId) board[y, x] = -1;
                }
            }
        }

        public List<ConsoleColor> colors = new List<ConsoleColor>()
        {
            ConsoleColor.Blue,
            ConsoleColor.Cyan,
            ConsoleColor.Green,
            ConsoleColor.Magenta,
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkRed,
            ConsoleColor.DarkYellow,
            ConsoleColor.DarkGray,
            ConsoleColor.Blue,
            ConsoleColor.Cyan,
            ConsoleColor.Green,
            ConsoleColor.Magenta,
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkMagenta,
            ConsoleColor.DarkRed,
            ConsoleColor.DarkYellow,
            ConsoleColor.DarkGray,
        };
        public void ShowBoard()
        {
            if (gameWidth == 0 || gameHeight == 0) return;
            ConsoleColor lastColor = ConsoleColor.White;
            for (int y = 0; y < gameHeight; y++)
            {
                for (int x = 0; x < gameWidth; x++)
                {
                    int p = board[y, x];
                    if (p == -1)
                    {
                        Console.Write("  ");
                        continue;
                    }
                    if (lastColor != colors[p])
                    {
                        lastColor = colors[p];
                        Console.ForegroundColor = lastColor;
                    }
                    Console.Write(p == playerId ? "##" : "██");
                    /*
                    if (IsPlayerHead(x, y, p))
                    {
                        Console.Write(p == playerId ? "##" : "██");
                    }
                    else
                    {
                        Console.Write(p == playerId ? "██" : "##");
                    }
                    */
                }
                Console.Write("\n");
            }
        }

        private bool IsPlayerHead(int x, int y, int playerId)
        {
            Vector2 head = players[GetPlayerIndexBasedOnId(playerId)].positions.Last();
            return (int)head.X == x && (int)head.Y == y;
        }

        public List<string> facts = new List<string>()
        {
            "Honey never spoils.",
            "Shortest war: 38-45 mins.",
            "Clouds weigh 1M pounds.",
            "Average person walks 5x world.",
            "Giraffes have 7 neck bones.",
            "Mexico has largest pyramid.",
            "Wait 6 months at red lights.",
            "Cows have best friends.",
            "Ozone layer: 3M pools.",
            "Fireflies' light is efficient.",
            "Strawberries aren't berries.",
            "Oysters change gender.",
            "Hawaii moves closer to Alaska.",
            "Avg. person blinks 15 times/min.",
            "More chess iterations than atoms.",
            "Octopuses have 3 hearts.",
            "World's quietest room: -9 dB.",
            "Group of flamingos: flamboyance.",
            "Great Wall not visible from space.",
            "Taste buds last 10 days.",
            "Brain stores 2.5 petabytes.",
            "Oldest tree: 5,000 years.",
            "Largest snowflake: 15 inches.",
            "Polar bears undetectable by IR.",
            "Snails sleep 3 years.",
            "Facts powered by ChatGPT",
            "The Eiffel Tower grows in summer.",
            "Cats have five toes on their front paws.",
            "Koalas sleep up to 22 hours a day.",
            "The moon is moving away from Earth.",
            "Ants never sleep.",
            "The average cloud weighs 1.1 million pounds.",
            "A crocodile can't stick its tongue out.",
            "The Earth's core is as hot as the sun's surface.",
            "Squirrels forget where they hide 50% of their nuts.",
            "The longest recorded flight of a chicken is 13 seconds.",
            "Sharks have been around longer than trees.",
            "Cheetahs can accelerate from 0 to 60 mph in 3 seconds.",
            "Elephants can recognize themselves in a mirror.",
            "The shortest war in history: 2 hours.",
            "Owls are the only birds that can see the color blue.",
            "The average person spends 6 months waiting for traffic lights.",
            "Horses can't vomit.",
            "The longest word in English has 189,819 letters.",
            "A snail can sleep for up to 3 years.",
            "The human eye can distinguish about 10 million different colors.",
            "The world's oldest known recipe is for beer.",
            "The speed of a computer mouse is measured in 'Mickeys.'",
            "The world's smallest mammal is the bumblebee bat.",
            "Penguins have an equivalent of knees in their legs.",
            "A group of pugs is called a grumble.",
            "The average person walks the equivalent of 3 times around the world in a lifetime.",
        };

        
        public string GetFortune()
        {
            return facts[Random.Shared.Next(0, facts.Count)];
            ProcessStartInfo p = new ProcessStartInfo
            {
                FileName = "fortune",
                RedirectStandardOutput = true
            };
            Process pp = Process.Start(p);
            return pp.StandardOutput.ReadToEnd();
        }

        public void SendMessage(string content)
        {
            Send("chat|" + content);
        }

        private void ComputeMove()
        {
            List<LookaheadMove> moves = new List<LookaheadMove>();
            Stopwatch s = Stopwatch.StartNew();
            List<Player> playerCopy = new List<Player>(players);
            int[,] boardCopy = (int[,])board.Clone();
            foreach (Player x in playerCopy)
            {
                x.predictionStartPos = x.positions.Last();
                if (x.playerId != playerId)
                {
                    x.BlockSourrounding(ref boardCopy, this);
                }
            }
            for (int i = 0; i < 20000; i++)
            {
                LookaheadMove m = new LookaheadMove();
                Vector2 currentPos = GetOwnPosition();
                Vector2 nextDirection = GetRandomDirection();
                m.direction = GetDirectionName(nextDirection);
                for (int t = 0; t < 150 && CheckMove(nextDirection, currentPos, boardCopy); t++)
                {
                    currentPos += nextDirection;
                    nextDirection = GetRandomDirection();
                    m.moves++;
                }

                if (players.Count(x => !x.dead) == 2)
                {
                    Player opponent = players.Find(x => !x.dead && x.playerId == playerId);
                    int distanceToOpponent = opponent.DistanceTo(currentPos);
                    m.score = m.moves / (float)distanceToOpponent; // flawed calculation. Prefeers getting to opponent instead of suriving. Will go into suicide position
                    m.score = m.moves;
                }
                else
                {
                    m.score = m.moves;
                }
                moves.Add(m);
            }
            LookaheadMove best = moves.OrderByDescending(x => x.score).First();
            nextMove = best.direction;
            Console.WriteLine("Computed next move in " + s.ElapsedMilliseconds + " ms");
        }

        public Vector2 GetRandomDirection()
        {
            return directions[Random.Shared.Next(0, directions.Count)];
        }

        public static List<Vector2> directions = new List<Vector2>
        {
            new Vector2(1, 0),
            new Vector2(-1, 0),
            new Vector2(0, 1),
            new Vector2(0, -1),
        };

        public bool CheckMove(Vector2 direction, Vector2 position, int[,] board)
        {
            return !IsPositionOccupied(position + direction, board);
        }

        public string GetDirectionName(Vector2 direction)
        {
            if (direction.X == -1) return "left";
            if (direction.X == 1) return "right";
            if (direction.Y == -1) return "up";
            if (direction.Y == 1) return "down";
            return "";
        }

        public void UpdatePlayerPosition(int playerId, int x, int y)
        {
            int i = GetPlayerIndexBasedOnId(playerId);
            players[i].positions.Add(new Vector2(x, y));
            board[y, x] = playerId;
        }

        public int GetPlayerIndexBasedOnId(int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].playerId == playerId) return i;
            }

            players.Add(new Player { playerId = playerId });
            return players.Count - 1;
        }

        public int ParseInt(string s)
        {
            return Convert.ToInt32(s);
        }

        public void Send(string s)
        {
            Console.WriteLine("Sending " + s);
            try
            {
                client.GetStream().Write(Encoding.ASCII.GetBytes(s + "\n"));
            }
            catch (Exception e)
            {
                
            }
        }

        public void Start()
        {
            Connect();
            Thread chatThread = new Thread(() =>
            {
                while (true)
                {
                    SendMessage(GetFortune());
                    Thread.Sleep(8000);
                }
            });
            //chatThread.Start();
            /*
             * Legacy move code
             
            while (true)
            {
                ConsoleKeyInfo k = Console.ReadKey(true);
                switch (k.Key)
                {
                    case ConsoleKey.W:
                        nextMove = "up";
                        break;
                    case ConsoleKey.A:
                        nextMove = "left";
                        break;
                    case ConsoleKey.S:
                        nextMove = "down";
                        break;
                    case ConsoleKey.D:
                        nextMove = "right";
                        break;
                }
            }
            */
        }
    }

    internal class LookaheadMove
    {
        public string direction;
        public int moves = 0;
        public float score = 0;
    }

    internal class Player
    {
        public int playerId = 0;
        public List<Vector2> positions = new List<Vector2>();
        public Vector2 predictionStartPos = new Vector2();
        public bool dead = false;

        public void AddMove(Vector2 direction)
        {
            positions.Add(positions.Last() + direction);
        }

        public void BlockSourrounding(ref int[,] board, Game g)
        {
            foreach (Vector2 d in Game.directions)
            {
                Vector2 x = g.GetVector2InPlayfield(positions.Last() + d);
                board[(int)x.Y, (int)x.X] = playerId;
            }
        }

        public int DistanceTo(Vector2 currentPos)
        {
            return (int)Math.Abs(Math.Round((currentPos - currentPos).Length()));
        }
    }
}