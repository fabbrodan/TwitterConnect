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

        private const string connectionString = "Server=localhost; Database=TwitterData; User ID=root; Password=Ester123";
        private static SqlConnection conn;

        static void Main(string[] args)
        {
            Auth.SetUserCredentials("hHiDu4helfSfNGi72mA9ADPV3",
                "sTYpnDH3m7eOXW84CaUnzxYa4MGBImAKVdgLO6QWZIddEDejEZ",
                "1629853610-0YqP6mkA1fBDy3RpPfXcgEvj9J1wCYm3pdjpeQC",
                "qQZqhIAQDQb0K0Q6ZCaFyyGyfmsyvvG3UOfBjCBaH11El");


            //Console.WriteLine("Enter tracking term");
            streamTerm = args[0];
            Console.WriteLine(streamTerm);
            string operation = "stream";
            while (operation != "exit" && operation == "stream")
            {
                if (!feedAlive)
                {
                    StreamSearch();
                }
                Thread.Sleep(1000);
                if(!feedAlive && !timerAlive)
                {
                    Console.WriteLine("Anything else? stream/exit");
                    operation = Console.ReadLine();
                    if (operation == "exit")
                    {
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Enter tracking term");
                        streamTerm = Console.ReadLine();
                    }
                }
                else if (feedAlive && timerAlive)
                {
                    if (counter%60 == 0 && counter != 0)
                    {
                        Console.WriteLine("Stream has been running for {0} minute(s).", counter/60);
                        Console.WriteLine("{0} tweets has matched.", rowCounter);
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
            string[] searchWords = new string[] { "sd", "svergie demokraterna", "sverigedemokraterna", "sd2018", "jimmie", "åkesson" };

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
                    rowCounter = 0;
                    counter = 0;
                    return;
                }
            }
        }

        static void CheckTweets(ITweet tweet, string[] searchWords, SqlConnection conn)
        {
            string tweetText = tweet.FullText.ToLower();

            List<string> Keywords = new List<string>();

            foreach (string word in searchWords)
            {
                if (tweetText.Contains(" " + word + " "))
                {
                    Keywords.Add(word);
                }

                if (tweet.QuotedTweet != null)
                {
                    if (tweet.QuotedTweet.FullText.ToLower().Contains(" " + word + " "))
                    {
                        Keywords.Add(word);
                    }
                }

                if (tweet.IsRetweet)
                {
                    if (tweet.RetweetedTweet.FullText.ToLower().Contains(" " + word + " "))
                    {
                        Keywords.Add(word);
                    }
                }
            }

            if (Keywords.Count > 0)
            {
                string keyWord = string.Empty;

                foreach (string keyword in Keywords)
                {
                    keyWord = keyword + "; ";
                }

                SqlCommand RetweetCmd = new SqlCommand();
                SqlCommand QuoteCmd = new SqlCommand();

                SqlCommand TweetCmd = new SqlCommand("INSERT INTO Tweets VALUES(" +
                "@UserID, @UserName, @UserLocation, @TweetID, @TweetText, @RetweetID, @QuoteID, @RetweetCount, @FavouriteCount, @QuoteCount, @Keywords, @Published, @URL);");

                TweetCmd.Parameters.AddWithValue(@"@UserID", tweet.CreatedBy.Id);
                TweetCmd.Parameters.AddWithValue(@"@UserName", tweet.CreatedBy.ToString());
                TweetCmd.Parameters.AddWithValue(@"@UserLocation", String.IsNullOrEmpty(tweet.CreatedBy.Location) ? (object)DBNull.Value : tweet.CreatedBy.Location);
                TweetCmd.Parameters.AddWithValue(@"@TweetID", tweet.Id);
                TweetCmd.Parameters.AddWithValue(@"@TweetText", tweet.FullText);
                TweetCmd.Parameters.AddWithValue(@"@RetweetID", tweet.IsRetweet ? tweet.RetweetedTweet.Id : (object)DBNull.Value);
                TweetCmd.Parameters.AddWithValue(@"@QuoteID", tweet.QuotedTweet == null ? (object)DBNull.Value : tweet.QuotedTweet.Id);
                TweetCmd.Parameters.AddWithValue(@"@RetweetCount", tweet.RetweetCount);
                TweetCmd.Parameters.AddWithValue(@"@FavouriteCount", tweet.FavoriteCount);
                TweetCmd.Parameters.AddWithValue(@"@Keywords", keyWord);
                TweetCmd.Parameters.AddWithValue(@"@QuoteCount", tweet.QuoteCount);
                TweetCmd.Parameters.AddWithValue(@"@Published", tweet.CreatedAt);
                TweetCmd.Parameters.AddWithValue(@"@URL", tweet.Url);

                if (tweet.IsRetweet)
                {
                    RetweetCmd.CommandText = "INSERT INTO Retweets VALUES(" +
                        "@UserID, @UserName, @UserLocation, @TweetID, @TweetText, @RetweetCount, @FavouriteCount, @QuoteCount, @Published, @URL);";

                    RetweetCmd.Parameters.AddWithValue(@"@UserID", tweet.RetweetedTweet.CreatedBy.Id);
                    RetweetCmd.Parameters.AddWithValue(@"@UserName", tweet.RetweetedTweet.CreatedBy.ToString());
                    RetweetCmd.Parameters.AddWithValue(@"@UserLocation", String.IsNullOrEmpty(tweet.RetweetedTweet.CreatedBy.Location) ? (object)DBNull.Value : tweet.RetweetedTweet.CreatedBy.Location);
                    RetweetCmd.Parameters.AddWithValue(@"@TweetID", tweet.RetweetedTweet.Id);
                    RetweetCmd.Parameters.AddWithValue(@"@TweetText", tweet.RetweetedTweet.FullText);
                    RetweetCmd.Parameters.AddWithValue(@"@RetweetCount", tweet.RetweetedTweet.RetweetCount);
                    RetweetCmd.Parameters.AddWithValue(@"@FavouriteCount", tweet.RetweetedTweet.FavoriteCount);
                    RetweetCmd.Parameters.AddWithValue(@"@QuoteCount", tweet.RetweetedTweet.QuoteCount);
                    RetweetCmd.Parameters.AddWithValue(@"@Published", tweet.RetweetedTweet.CreatedAt);
                    RetweetCmd.Parameters.AddWithValue(@"@URL", tweet.RetweetedTweet.Url);
                }

                if (tweet.QuotedTweet != null)
                {
                    QuoteCmd.CommandText = "INSERT INTO QuotedTweets VALUES(" +
                        "@UserID, @UserName, @UserLocation, @TweetID, @TweetText, @RetweetCount, @FavouriteCount, @QuotedCount, @Published, @URL);";

                    QuoteCmd.Parameters.AddWithValue(@"@UserID", tweet.QuotedTweet.CreatedBy.Id);
                    QuoteCmd.Parameters.AddWithValue(@"@UserName", tweet.QuotedTweet.CreatedBy.ToString());
                    QuoteCmd.Parameters.AddWithValue(@"@UserLocation", String.IsNullOrEmpty(tweet.QuotedTweet.CreatedBy.Location) ? (object)DBNull.Value : tweet.QuotedTweet.CreatedBy.Location);
                    QuoteCmd.Parameters.AddWithValue(@"@TweetID", tweet.QuotedTweet.Id);
                    QuoteCmd.Parameters.AddWithValue(@"@TweetText", tweet.QuotedTweet.FullText);
                    QuoteCmd.Parameters.AddWithValue(@"@RetweetCount", tweet.QuotedTweet.RetweetCount);
                    QuoteCmd.Parameters.AddWithValue(@"@FavouriteCount", tweet.QuotedTweet.FavoriteCount);
                    QuoteCmd.Parameters.AddWithValue(@"@QuotedCount", tweet.QuotedTweet.QuoteCount);
                    QuoteCmd.Parameters.AddWithValue(@"@Published", tweet.QuotedTweet.CreatedAt);
                    QuoteCmd.Parameters.AddWithValue(@"@URL", tweet.QuotedTweet.Url);
                }

                try
                {
                    conn.Open();

                    TweetCmd.Connection = conn;

                    TweetCmd.ExecuteNonQuery();

                    if (tweet.IsRetweet)
                    {
                        RetweetCmd.Connection = conn;

                        using (SqlCommand cmd = new SqlCommand())
                        {
                            cmd.Connection = conn;
                            cmd.CommandText = "SELECT 1 FROM Retweets WHERE TweetID = " + tweet.RetweetedTweet.Id + ";";
                            SqlDataReader reader = cmd.ExecuteReader();
                            if (reader.HasRows)
                            {
                                reader.Close();

                                RetweetCmd.CommandText = "UPDATE Retweets " +
                                    "SET RetweetCount = " + tweet.RetweetedTweet.RetweetCount + ", " +
                                    "FavouriteCount = " + tweet.RetweetedTweet.FavoriteCount + ", " +
                                    "QuoteCount = " + tweet.RetweetedTweet.QuoteCount +
                                    " WHERE TweetID = " + tweet.RetweetedTweet.Id + ";";

                                RetweetCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                reader.Close();
                                RetweetCmd.ExecuteNonQuery();
                            }
                        }

                    }
                    if (tweet.QuotedTweet != null)
                    {
                        QuoteCmd.Connection = conn;

                        using (SqlCommand cmd = new SqlCommand())
                        {
                            cmd.Connection = conn;
                            cmd.CommandText = "SELECT 1 FROM QuotedTweets WHERE TweetID = " + tweet.QuotedTweet.Id + ";";
                            SqlDataReader reader = cmd.ExecuteReader();
                            if (reader.HasRows)
                            {
                                reader.Close();

                                QuoteCmd.CommandText = "UPDATE QuotedTweets " +
                                    "SET RetweetCount = " + tweet.QuotedTweet.RetweetCount + ", " +
                                    "FavouriteCount = " + tweet.QuotedTweet.FavoriteCount + ", " +
                                    "QuotedCount = " + tweet.QuotedTweet.QuoteCount +
                                    " WHERE TweetID = " + tweet.QuotedTweet.Id + ";";

                                QuoteCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                reader.Close();
                                QuoteCmd.ExecuteNonQuery();
                            }
                        }
                    }
                    
                    rowCounter++;
                }
                catch (SqlException exc)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine(exc.Message);
                }
                finally
                {
                    conn.Close();
                }
            }
        }
    }
}