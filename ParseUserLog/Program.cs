using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace ParseUserLog
{
    using static RegexOptions;
    using static Console;
    using static String;

    static class Program
    {
        #region Methods

        static void Main(string[] args)
        {
            IDictionary<int, List<Stage>> stages = new Dictionary<int, List<Stage>>
            {
                [0] = new List<Stage>(),
                [3] = new List<Stage>(),
                [4] = new List<Stage>(),
                [5] = new List<Stage>(),
            };
            try
            {
                var stopwatch = Stopwatch.StartNew();
                args.ForEach(stages.ParseFolderOrFile);
                stages.MergeStages()
                      .DumpStages();
                stopwatch.Stop();
                Error.WriteLine($@"Total time: {stopwatch.Elapsed:hh\:mm\:ss}");
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
                Error.WriteLine(ex.StackTrace);
            }
        }

        static void ParseFolderOrFile(this IDictionary<int, List<Stage>> stages, string path)
        {
            if (Directory.Exists(path))
            {
                Directory.GetFiles(path)
                         .ForEach(stages.ParseFolderOrFile);
                Directory.GetDirectories(path)
                         .ForEach(stages.ParseFolderOrFile);
            }
            else if (File.Exists(path))
            {
                if (LogFileNamePattern.IsMatch(path))
                {
                    if (new FileInfo(path).Length == 0)
                    {
                        Error.WriteLine($"*** Deleting empty file {path}");
                        File.Delete(path);
                    }
                    else
                    {
                        path.ParseFile()
                            .Traverse(stages);
                    }
                }
            }
            else
            {
                Error.WriteLine($"*** Neither a folder nor a file: {path}");
            }
        }

        static IDictionary<DateTime, Record> ParseFile(this string path)
        {
            Error.WriteLine($"*** parsing {path}");

            var result = new Dictionary<DateTime, Record>();
            string line;
            using (var file = new StreamReader(path))
            {
                while ((line = file.ReadLine()) != null)
                {
                    var match = LinePattern.Match(line);
                    if (!match.Success || match.Groups["level"].Value != "INFO")
                    {
                        continue;
                    }
                    var match2 = EventPattern.Match(match.Groups["message"].Value);
                    if (!match2.Success)
                    {
                        continue;
                    }
                    var record = Record.Parse(match2.Groups["json"].Value);
                    if (record != null && record.data.Canonicalize())
                    {
                        result[DateTime.Parse(match.Groups["time"].Value)] = record;
                    }
                }
            }
            if (result.Count == 0)
            {
                Error.WriteLine("    Deleting the file because it contains no useful data");
                File.Delete(path);
            }
            return result;
        }

        static void Traverse(this IDictionary<DateTime, Record> records, IDictionary<int, List<Stage>> stages)
        {
            if (records.Count == 0)
            {
                return;
            }

            Error.WriteLine("*** traversing");

            var endResults = records.GetGameOverResults();
            int total = records.Count(pair => pair.Value.eventName == EventShowAction);
            int i = 0;
            int percent = 0;
            foreach (var pair in from pair in records
                                 where pair.Value.eventName == EventShowAction
                                 orderby pair.Key
                                 select pair)
            {
                var data = pair.Value.data;
                var player = data.CurrentPlayer;

                void UpdateRank(double rank)
                {
                    var stage = stages[data.table.board.Length].FindSameStage(player.Cards, data.table.Board, data.action.action);
                    if (stage == null)
                    {
                        stages[data.table.board.Length].Add(
                            new Stage
                            {
                                cards = player.Cards,
                                board = data.table.Board,
                                action = data.action.action,
                                rank = rank,
                                count = 1,
                            }
                        );
                    }
                    else
                    {
                        stage.rank += rank;
                        stage.count++;
                    }
                }

                endResults.UpdateWinnerRank(time: pair.Key, table: data.table, playerName: player?.playerName, updateRank: UpdateRank);
                i++;
                int p = i * 100 / total;
                if (p > percent)
                {
                    percent = p;
                    Error.Write($"{percent}%\r");
                }
            }
            Error.WriteLine();
        }

        static IDictionary<int, List<Stage>> MergeStages(this IDictionary<int, List<Stage>> stages)
        {
            Error.WriteLine("*** merging");

            var result = new Dictionary<int, List<Stage>>();
            foreach (var pair in stages)
            {
                var buffer = new List<Stage>();
                Stage last = null;
                foreach (var stage in pair.Value.OrderBy(stage => stage.Order))
                {
                    if (last == null)
                    {
                        last = stage;
                    }
                    else if (last.AllCards != stage.AllCards)
                    {
                        buffer.Add(last);
                        last = stage;
                    }
                    else if (last.AverageRank < stage.AverageRank)
                    {
                        last = stage;
                    }
                }
                if (last != null)
                {
                    buffer.Add(last);
                }
                result[pair.Key] = buffer;
            }
            return result;
        }

        static void DumpStages(this IDictionary<int, List<Stage>> stages)
        {
            Error.WriteLine("*** dumping");
            WriteLine("Cards,Board,Action,AverageRank,Count");
            foreach (var pair in stages)
            {
                foreach (var stage in pair.Value.OrderBy(stage => stage.Order))
                {
                    WriteLine($"\"{stage.cards}\",\"{stage.board}\",{stage.action},{stage.AverageRank},{stage.count}");
                }
            }
        }

        static IDictionary<DateTime, Record> GetGameOverResults(this IDictionary<DateTime, Record> records) =>
            (from pair in records
             where pair.Value.eventName == EventGameOver
             select pair
            ).ToDictionary(
                keySelector: pair => pair.Key,
                elementSelector: pair => pair.Value
            );

        static void UpdateWinnerRank(this IDictionary<DateTime, Record> endResults, DateTime time, Table table, string playerName, Action<double> updateRank)
        {
            var winner = (from pair in endResults
                          orderby pair.Key
                          let data = pair.Value.data
                          where pair.Key > time && data.table.Same(table) && data.FindPlayer(playerName) != null
                          select data
                         ).FirstOrDefault()
                         ?.FindWinner(playerName);
            if (winner != null)
            {
                updateRank(winner.hand.rank);
            }
        }

        static Stage FindSameStage(this List<Stage> stages, string cards, string board, string action) =>
            stages.FirstOrDefault(stage => stage.cards == cards && stage.board == board && stage.action == action);

        static Regex AsPattern(this string value) => new Regex(value, Compiled | CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline);

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) =>
            collection?.ToList()
                       .ForEach(action);

        static string[] SortCards(this string[] cards) => cards?.OrderBy(card => card)
                                                                .ToArray() ?? new string[0];

        #endregion Methods


        #region Nested types

        [DataContract]
        public class Record
        {
            [DataMember] public string eventName = null;
            [DataMember] public Data data = null;

            public static Record Parse(string json)
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return new DataContractJsonSerializer(typeof(Record)).ReadObject(stream) as Record;
                }
            }
        }

        [DataContract]
        public class Data
        {
            [DataMember] public Player[] players = null;
            [DataMember] public Table table = null;
            [DataMember] public Action action = null;
            [DataMember] public Winner[] winners = null;

            public Player FindPlayer(string playerName) => players?.FirstOrDefault(player => player.playerName == playerName);
            public Winner FindWinner(string playerName) => winners?.FirstOrDefault(winner => winner.playerName == playerName);

            public Player CurrentPlayer => FindPlayer(action?.playerName);

            public bool Canonicalize()
            {
                table?.Canonicalize();
                return players?.All(player => player.Canonicalize()) == true;
            }
        }

        [DataContract]
        public class Player
        {
            [DataMember] public string playerName = null;
            [DataMember] public int chips = 0;
            [DataMember] public bool folded = false;
            [DataMember] public bool allIn = false;
            [DataMember] public string[] cards = null;
            [DataMember] public bool isSurvive = false;
            [DataMember] public int reloadCount = 0;
            [DataMember] public int roundBet = 0;
            [DataMember] public int bet = 0;
            [DataMember] public bool isOnline = false;
            [DataMember] public bool isHuman = false;

            public string Cards = null;

            public bool Canonicalize()
            {
                Cards = Join(",", cards.SortCards());
                return Cards.Length == 5; // expecting "XX,YY"
            }
        }

        [DataContract]
        public class Table
        {
            [DataMember] public int tableNumber = 0;
            [DataMember] public int status = 0;
            [DataMember] public string roundName = null;
            [DataMember] public string[] board = null; // 0,3,4,5
            [DataMember] public int roundCount = 0;
            [DataMember] public int raiseCount = 0;
            [DataMember] public int betCount = 0;
            [DataMember] public int totalBet = 0;
            [DataMember] public int initChips = 0;
            [DataMember] public int maxReloadCount = 0;
            [DataMember] public Blind smallBlind = null;
            [DataMember] public Blind bigBlind = null;

            public string Board = null;

            public void Canonicalize()
            {
                Board = Join(",", board.SortCards());
            }

            public bool Same(Table other) => tableNumber == other.tableNumber && smallBlind.playerName == other.smallBlind.playerName && bigBlind.playerName == other.bigBlind.playerName;
        }

        [DataContract]
        public class Blind
        {
            [DataMember] public string playerName = null;
            [DataMember] public int amount = 0;
        }

        [DataContract]
        public class Action
        {
            [DataMember] public string action = null;
            [DataMember] public string playerName = null;
            [DataMember] public int amount = 0;
            [DataMember] public int chips = 0;
        }

        [DataContract]
        public class Winner
        {
            [DataMember] public string playerName = null;
            [DataMember] public Hand hand = null;
            [DataMember] public int chips = 0;
        }

        [DataContract]
        public class Hand
        {
            [DataMember] public string[] cards = null;
            [DataMember] public double rank = 0.0;
            [DataMember] public string message = null;
        }

        public class Stage
        {
            public string cards;
            public string board;
            public string action;
            public double rank;
            public int count;

            public double AverageRank => rank / count;
            public string AllCards => cards + ";" + board;
            public string Order => AllCards + ";" + action;
        }

        #endregion Nested types


        #region Data fiedls

        const string EventShowAction = "__show_action";
        const string EventRoundEnd = "__round_end";
        const string EventGameOver = "__game_over";

        static readonly Regex LinePattern = @"\A
                                              \[ (?<time> \d{4}-\d{2}-\d{2} T \d{2}:\d{2}:\d{2} \. \d{3} ) \] \s+ # time
                                              \[ (?<level> \w+ ) \] \s+                                           # message level, eg. ERROR
                                              (?<type> \w+ ) \s+                                                  # type of log, eg. userDebugLog
                                              - \s+
                                              (?<time2> \d{4}-\d{2}-\d{2} \s+ \d{2}:\d{2}:\d{2} \s+ \d{1,3} ) \s* # time (probably starting time)
                                              : \s+
                                              (?<message> .+ )                                                    # message
                                              \Z".AsPattern();
        static readonly Regex EventPattern = @"\A
                                               \s*>>> \s+ event \s+ (?<event> \w+) \s+ >>> \s+ # event  token
                                               (?<json> .+ )                                   # event content, should be a JSON structure
                                               \Z".AsPattern();
        static readonly Regex LogFileNamePattern = @"\.log (\.\d+)? \Z".AsPattern();

        #endregion Data fields
    }
}
