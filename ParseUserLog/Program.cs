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

    static class Program
    {
        #region Methods

        static void Main(string[] args)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using (var connection = new SqlConnection(@"Server=localhost\SQLEXPRESS;Database=TEXASLOG;Trusted_Connection=True;"))
                {
                    connection.Open();
                    args.Where(arg => arg[0] != '-')
                        .ForEach(connection.ParseFolderOrFile);
                    if (args.Any(arg => arg == "-d"))
                    {
                        connection.DumpDigested();
                    }
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

        static void ParseFolderOrFile(this SqlConnection connection, string path)
        {
            if (Directory.Exists(path))
            {
                Directory.GetFiles(path)
                         .ForEach(connection.ParseFolderOrFile);
                Directory.GetDirectories(path)
                         .ForEach(connection.ParseFolderOrFile);
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
                            .TraverseAndSaveDigested(connection);
                    }
                }
            }
            else
            {
                Error.WriteLine($"*** Neither a folder nor a file: {path}");
            }
        }

        static (Records, Records) ParseFile(this string path)
        {
            Error.WriteLine($"*** parsing {path}");

            var showActionResult = new Records();
            var gameOverResult = new Records();
            string line;
            int ln = 0;
            using (var file = new StreamReader(path))
            {
                while ((line = file.ReadLine()) != null)
                {
                    ln++;
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
                    var record = Record.Parse(match2.Groups["json"].Value, path, ln);
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

        static void TraverseAndSaveDigested(this (Records, Records) parseResult, SqlConnection connection)
        {
            var (showActionRecords, gameOverRecords) = parseResult;
            if (showActionRecords.Count == 0 || gameOverRecords.Count == 0)
            {
                return;
            }

            Error.WriteLine("*** traversing");

            Winner FindWinner(DateTime time, Table table, string playerName) =>
                (from pair in gameOverRecords
                 orderby pair.Key
                 let data = pair.Value.data
                 where pair.Key > time && data.table.Same(table) && data.FindPlayer(playerName) != null
                 select data
                ).FirstOrDefault()
                ?.FindWinner(playerName);

            int total = showActionRecords.Count;
            int i = 0;
            int percent = 0;
            foreach (var pair in showActionRecords.OrderBy(pair => pair.Key))
            {
                var data = pair.Value.data;
                var player = data.CurrentPlayer;
                var winner = FindWinner(time: pair.Key, table: data.table, playerName: player?.playerName);
                if (winner != null)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"IF NOT EXISTS( SELECT * FROM [DIGESTED] WHERE [TIME] = @time )
                                                INSERT [DIGESTED] ([TIME], [CARDS], [BOARD], [ACTION], [RANK])
                                                VALUES (@time, @cards, @board, @action, @rank)";
                        command.Parameters.AddWithValue("time", pair.Key);
                        command.Parameters.AddWithValue("cards", player.Cards);
                        command.Parameters.AddWithValue("board", data.table.Board);
                        command.Parameters.AddWithValue("action", data.action.action);
                        command.Parameters.AddWithValue("rank", winner.hand.rank);
                        command.ExecuteNonQuery();
                    }
                }
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

        static void DumpDigested(this SqlConnection connection)
        {
            Error.WriteLine("*** dumping");

            WriteLine("Cards,Board,Action,AverageRank,Count");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT   [CARDS], [BOARD], [ACTION], [AVG_RANK] = AVG([RANK]), [COUNT] = SUM(1)
                                        FROM     [DIGESTED]
                                        GROUP BY [CARDS], [BOARD], [ACTION]
                                        ORDER BY LEN([BOARD]) ASC, [CARDS] ASC, [BOARD] ASC";
                using (var reader = command.ExecuteReader())
                {
                    Digested last = null;
                    while (reader.Read())
                    {
                        var digested = new Digested
                        {
                            cards = reader["CARDS"] as string,
                            board = reader["BOARD"] as string,
                            action = reader["ACTION"] as string,
                            averageRank = (double)reader["AVG_RANK"],
                            count = (int)reader["COUNT"],
                        };
                        if (last == null)
                        {
                            last = digested;
                        }
                        else if (last.AllCards != digested.AllCards)
                        {
                            WriteLine($"\"{last.cards}\",\"{last.board}\",{last.action},{last.averageRank},{last.count}");
                            last = digested;
                        }
                        else if (last.averageRank < digested.averageRank)
                        {
                            last = digested;
                        }
                    }
                    if (last != null)
                    {
                        WriteLine($"\"{last.cards}\",\"{last.board}\",{last.action},{last.averageRank},{last.count}");
                    }
                }
            }
        }

        static Regex AsPattern(this string value) => new Regex(value, Compiled | CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline);

        static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) =>
            collection?.ToList()
                       .ForEach(action);

        public static string[] SortCards(this string[] cards) => cards?.OrderBy(card => card)
                                                                       .ToArray() ?? new string[0];

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
