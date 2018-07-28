using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ParseUserLog
{
    using static RegexOptions;
    using static Console;
    using Records = Dictionary<DateTime, Record>;
    using Stages = Dictionary<int, List<Stage>>;

    static class Program
    {
        #region Methods

        static void Main(string[] args)
        {
            var stages = new Stages
            {
                [0] = new List<Stage>(),
                [3] = new List<Stage>(),
                [4] = new List<Stage>(),
                [5] = new List<Stage>(),
            };
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using (var connection = new SqlConnection(@"Server=localhost\SQLEXPRESS;Database=TEXASLOG;Trusted_Connection=True;"))
                {
                    connection.Open();
                    args.ForEach(arg => stages.ParseFolderOrFile(arg, connection));
                    stages.MergeStages()
                          .DumpStages();
                }
                stopwatch.Stop();
                Error.WriteLine($@"Total time: {stopwatch.Elapsed:hh\:mm\:ss}");
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
                Error.WriteLine(ex.StackTrace);
            }
        }

        static void ParseFolderOrFile(this Stages stages, string path, SqlConnection connection)
        {
            if (Directory.Exists(path))
            {
                Directory.GetFiles(path)
                         .ForEach(filePath => stages.ParseFolderOrFile(filePath, connection));
                Directory.GetDirectories(path)
                         .ForEach(subdir => stages.ParseFolderOrFile(subdir, connection));
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
                        path.ParseFile(connection)
                            .Traverse(stages);
                    }
                }
            }
            else
            {
                Error.WriteLine($"*** Neither a folder nor a file: {path}");
            }
        }

        static (Records, Records) ParseFile(this string path, SqlConnection connection)
        {
            Error.WriteLine($"*** parsing {path}");

            var showActionResult = new Records();
            var gameOverResult = new Records();
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
                    if (!match2.Success || !new[] { "SHOW_ACTION", "GAME_OVER" }.Contains(match2.Groups["event"].Value))
                    {
                        continue;
                    }
                    var record = Record.Parse(match2.Groups["json"].Value);
                    if (record != null && record.data.Canonicalize())
                    {
                        switch (match2.Groups["event"].Value)
                        {
                            case "SHOW_ACTION":
                                showActionResult[DateTime.Parse(match.Groups["time"].Value)] = record;
                                break;
                            case "GAME_OVER":
                                gameOverResult[DateTime.Parse(match.Groups["time"].Value)] = record;
                                break;
                        }
                    }
                }
            }
            if (showActionResult.Count == 0 && gameOverResult.Count == 0)
            {
                Error.WriteLine("    Deleting the file because it contains no useful data");
                File.Delete(path);
            }
            return (showActionResult, gameOverResult);
        }

        static void Traverse(this (Records, Records) parseResult, Stages stages)
        {
            var (showActionRecords, gameOverRecords) = parseResult;
            if (showActionRecords.Count == 0 || gameOverRecords.Count == 0)
            {
                return;
            }

            Error.WriteLine("*** traversing");

            int total = showActionRecords.Count;
            int i = 0;
            int percent = 0;
            foreach (var pair in showActionRecords.OrderBy(pair => pair.Key))
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

                gameOverRecords.UpdateWinnerRank(time: pair.Key, table: data.table, playerName: player?.playerName, updateRank: UpdateRank);
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

        static Stages MergeStages(this Stages stages)
        {
            Error.WriteLine("*** merging");

            var result = new Stages();
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

        static void DumpStages(this Stages stages)
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

        static void UpdateWinnerRank(this Records gameOverRecords, DateTime time, Table table, string playerName, Action<double> updateRank)
        {
            var winner = (from pair in gameOverRecords
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

        static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) =>
            collection?.ToList()
                       .ForEach(action);

        public static string[] SortCards(this string[] cards) => cards?.OrderBy(card => card)
                                                                       .ToArray() ?? new string[0];

        public static bool IsDBNull(this object value) => value == null || value == DBNull.Value;

        #endregion Methods


        #region Data fiedls

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
                                               \s*>>> \s+ event \s+ (?<event> \w+) \s+ >>> \s+ # event token
                                               (?<json> .+ )                                   # event content, should be a JSON structure
                                               \Z".AsPattern();
        static readonly Regex LogFileNamePattern = @"\.log (\.\d+)? \Z".AsPattern();

        #endregion Data fields
    }
}
