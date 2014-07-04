using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;


namespace BTCChina
{
    class Program
    {
        /// <summary>
        /// Takes a file as argument, whose
        /// first line is access key (public key) of your account, and
        /// second line is secret key of your account.
        /// </summary>
        /// <param name="args">name of the file</param>
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: BTCChina.exe account_filename");
            }
            else
                if (File.Exists(args[0]))
                {
                    using (StreamReader sr = File.OpenText(args[0]))
                    {
                        string accesskey = sr.ReadLine();
                        string secretkey = sr.ReadLine();
                        BTCChinaAPI testAPI = new BTCChinaAPI(accesskey, secretkey);
                        try
                        {
                            string result1 = testAPI.getAccountInfo();
                            string result2 = testAPI.getOrders(false);
                            Console.WriteLine(result2);
                            Console.WriteLine(result1);
                        }
                        catch (BTCChinaException ex)
                        {
                            Console.WriteLine("\nin method:" + ex.RequestMethod + "\nid:" + ex.RequestID + "\nmessage:" + ex.Message);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Error reading acc.txt");
                }
        }
    }
}
