using System;
using System.IO;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace TwitterConnect
{
    class Program
    {
        private static System.Timers.Timer timer;
        private static string streamTerm;
        private static int counter = 0;
        private static int rowCounter = 0;
        private static CancellationTokenSource cts;
        private static bool feedAlive;
        private static bool timerAlive;

        private const string connectionString = "Server=localhost\\SQLEXPRESS; Initial Catalog=TwitterData; User ID=sa; Password=Password123";
        private static SqlConnection conn;

        static void Main(string[] args)
        {
            Auth.SetUserCredentials("hHiDu4helfSfNGi72mA9ADPV3",
                "sTYpnDH3m7eOXW84CaUnzxYa4MGBImAKVdgLO6QWZIddEDejEZ",
                "1629853610-0YqP6mkA1fBDy3RpPfXcgEvj9J1wCYm3pdjpeQC",
                "qQZqhIAQDQb0K0Q6ZCaFyyGyfmsyvvG3UOfBjCBaH11El");

            
            Console.WriteLine("Enter tracking term");
            streamTerm = Console.ReadLine();
            string operation = string.Empty;
            while (operation != "exit")
            {
                if (!feedAlive)
                {
                    StreamSearch();
                }
                Thread.Sleep(1000);
                if(!feedAlive && !timerAlive)
                {
                    Console.WriteLine("Anything else? stream/search/exit");
                    operation = Console.ReadLine();
                }
                else if (feedAlive && timerAlive)
                {
                    if (counter%60 == 0 && counter != 0)
                    {
                        Console.WriteLine("Stream has been going for {0} minutes.", counter/60);
                        Console.WriteLine("{0} rows has been inserted.", rowCounter);
                    }
                }
            }
        }

        static void StreamSearch()
        {
            cts = new CancellationTokenSource();

            new Thread(() =>
            {
                Work(cts.Token);
            }).Start();
            
            new Thread(() =>
            {
                Timing(cts.Token);
            }).Start();

            new Thread(() =>
            {
                CheckForInput();
            }).Start();
        }

        private static void CheckForInput()
        {
            if(Console.ReadLine() == "q")
            {
                Console.WriteLine("Stopping stream...");
                cts.Cancel();
                return;
            }
        }

        private static void Timing(CancellationToken cancellationToken)
        {
            timerAlive = true;

            timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Elapsed += (sender, args) =>
            {
                counter++;
            };
            timer.Start();

            while (true)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    timer.Stop();
                    timerAlive = false;
                    return;
                }
            }
        }

        private static void Work(CancellationToken cancellationToken)
        {
            string[] searchWords = new string[] { "sd", "svergie", "demokraterna", "sverigedemokraterna", "2018", "sd2018", "jimmie", "åkesson" };

            conn = new SqlConnection(connectionString);

            feedAlive = true;

            var stream = Tweetinvi.Stream.CreateFilteredStream();
            stream.AddTrack(streamTerm);

            stream.MatchingTweetReceived += (sender, arg) =>
            {
                CheckTweets(arg.Tweet, searchWords, conn);
            };

            stream.StartStreamMatchingAllConditionsAsync();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    stream.StopStream();
                    feedAlive = false;
                    Console.WriteLine("Rows inserted: {0}", rowCounter);
                    Console.WriteLine("Streamed for {0} seconds.", counter);
                    return;
                }
            }
        }

        static void CheckTweets(ITweet tweet, string[] searchWords, SqlConnection conn)
        {
            string tweetText = tweet.Text.ToLower();

            foreach (string word in searchWords)
            {
                if (tweetText.Contains(word))
                {
                    SqlCommand cmd = new SqlCommand("INSERT INTO SD_STATS" +
                        " VALUES(" +
                        "@UserName, @IsRetweet, @OriginalPoster, @OriginalTweetedTime, @Retweets, @Favourites, @OriginalTweetText, @TweetText, @TweetedTime, @Keyword);"
                        , conn);

                    cmd.Parameters.AddWithValue(@"@UserName", tweet.CreatedBy.ToString());
                    cmd.Parameters.AddWithValue(@"@IsRetweet", tweet.IsRetweet ? 1 : 0);
                    cmd.Parameters.AddWithValue(@"@OriginalPoster", tweet.IsRetweet ? tweet.RetweetedTweet.CreatedBy.ToString() : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(@"@OriginalTweetedTime", tweet.IsRetweet ? tweet.RetweetedTweet.CreatedAt : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(@"@Retweets", tweet.IsRetweet ? tweet.RetweetedTweet.RetweetCount : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(@"@Favourites", tweet.IsRetweet ? tweet.RetweetedTweet.FavoriteCount : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(@"@OriginalTweetText", tweet.IsRetweet ? tweet.RetweetedTweet.FullText : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue(@"@TweetText", tweet.FullText);
                    cmd.Parameters.AddWithValue(@"@TweetedTime", tweet.CreatedAt);
                    cmd.Parameters.AddWithValue(@"@Keyword", word);

                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        rowCounter++;
                    }
                    catch (SqlException exc)
                    {
                        Debug.WriteLine(exc.Message);
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
        }
    }
}
