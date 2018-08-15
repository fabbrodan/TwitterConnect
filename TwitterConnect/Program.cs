using System;
using System.IO;
using System.Text;
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
        private static CancellationTokenSource cts;
        private static bool feedAlive;
        private static bool timerAlive;

        static void Main(string[] args)
        {
            Auth.SetUserCredentials("hHiDu4helfSfNGi72mA9ADPV3",
                "sTYpnDH3m7eOXW84CaUnzxYa4MGBImAKVdgLO6QWZIddEDejEZ",
                "1629853610-0YqP6mkA1fBDy3RpPfXcgEvj9J1wCYm3pdjpeQC",
                "qQZqhIAQDQb0K0Q6ZCaFyyGyfmsyvvG3UOfBjCBaH11El");

            
            Console.WriteLine("Livestream or search?");
            string operation = Console.ReadLine();
            while (operation != "exit")
            {
                if (operation == "stream" && !feedAlive)
                {
                    StreamSearch();
                }
                else if (operation == "search")
                {
                    StandardSearch();
                }
                Thread.Sleep(5000);
                if(!feedAlive && !timerAlive)
                {
                    Console.WriteLine("Anything else? stream/search/exit");
                    operation = Console.ReadLine();
                }
            }
        }

        static void StreamSearch()
        {
            Console.WriteLine("For what do you wanna stream?");
            streamTerm = Console.ReadLine();
            
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
                Debug.WriteLine(counter);
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
                else if (counter == 60)
                {
                    timer.Stop();
                    Console.WriteLine("Streaming stopped after 60 seconds.");
                    timerAlive = false;
                    return;
                }
            }
        }

        private static void Work(CancellationToken cancellationToken)
        {
            feedAlive = true;

            var stream = Tweetinvi.Stream.CreateFilteredStream();
            stream.AddTrack(streamTerm);

            stream.MatchingTweetReceived += (sender, arg) =>
            {
                CheckTweets(arg.Tweet);
            };

            stream.StartStreamMatchingAllConditionsAsync();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested || counter == 60)
                {
                    stream.StopStream();
                    feedAlive = false;
                    return;
                }
            }
        }

        static void StandardSearch()
        {
            Console.WriteLine("What do you want to find?");
            string searchTerm = Console.ReadLine();
            var searchParams = new SearchTweetsParameters(searchTerm)
            {
                SearchType = SearchResultType.Recent
            };

            var results = Search.SearchTweets(searchParams);

            foreach(ITweet tweet in results)
            {
                Console.WriteLine("------------------------------------------------------------");
                Console.WriteLine(tweet.CreatedBy);
                Console.WriteLine(tweet.Text);
                Console.WriteLine("------------------------------------------------------------");
            }
        }

        static void CheckTweets(ITweet tweet)
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine(tweet.CreatedBy);
            Console.WriteLine(tweet.Text);
            Console.WriteLine("------------------------------------------------------------");
        }
    }
}
