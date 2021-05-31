using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace metaExchange
{
    class Program
    {
        /// <summary>
        /// OrderBookData serialization classes
        /// </summary>
        public class Order
        {
            public object Id { get; set; }
            public DateTime Time { get; set; }
            public string Type { get; set; }
            public string Kind { get; set; }
            public double Amount { get; set; }
            public double Price { get; set; }
        };

        public class Bid
        {
            public Order Order { get; set; }
        }

        public class Ask
        {
            public Order Order { get; set; }
        }

        public class OrderBookData
        {

            public DateTime AcqTime { get; set; }
            public Bid[] Bids { get; set; }
            public Ask[] Asks { get; set; }

        };
        // End of OrderBookData serialization classes

        public class AssetPairBalance
        {
            public double BaseAsset { get; set; } // first asset in pair (BTC)
            public double QuotedAsset { get; set; } // second asset in pair (EUR)
        };

        public class User
        {
            int Id { get; set; }
            public Dictionary<string, AssetPairBalance> assetPairBalance; // i.e. BTC-EUR       
        };


        // Methods
        OrderBookData ReadOrderBookJSON(int id, string fileName)
        {
            //string fileName = "orderbooksample.json";                        
            string[] line = File.ReadAllLines(fileName);

            var jsonString = Regex.Replace(line[id], @"[+-]?([0-9]*[.])?[0-9]+\t", string.Empty);

            //var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Deserialize<OrderBookData>(jsonString);

        }
        
        void ExchangePair(User user, string orderType, string pairTicker, double amount, double price)
        {
            if (orderType == "sell")            
                amount *= -1.0;
            
            // Update user balance
            user.assetPairBalance[pairTicker].QuotedAsset -= amount*price; // i.e. EUR
            user.assetPairBalance[pairTicker].BaseAsset += amount; // i.e. BTC                        
            
        }
        
        string PlaceOrder(OrderBookData orderBook, User user, string pairTicker, string orderType, double orderAmount)
        {            
            string orderString = ""; //order syntax create_order(type, amount) ... 
            double amount = orderAmount;
            double avgCost = 0.0;

            // Place orders           
            if (orderType == "buy")
            {
                // Go trough each order in the orderBook to calculate buying cost
                foreach (Bid bid in orderBook.Bids)
                {
                    if (amount > bid.Order.Amount)
                    {
                        avgCost += bid.Order.Amount * bid.Order.Price;
                        amount -= bid.Order.Amount;
                    }
                    else
                    {
                        avgCost += amount * bid.Order.Price;
                        break;
                    }
                }

                if (avgCost > user.assetPairBalance[pairTicker].QuotedAsset)
                {
                    Console.WriteLine("Order amount too high! Adjusting to max. possible");
                    amount = user.assetPairBalance[pairTicker].QuotedAsset * (orderAmount / avgCost);
                }
                else
                    amount = orderAmount;

                // BUY
                double bidCost;
                foreach (Bid bid in orderBook.Bids)
                {
                    bidCost = bid.Order.Amount * bid.Order.Price;
                    if ( user.assetPairBalance[pairTicker].QuotedAsset > bidCost)
                    {
                        ExchangePair(user, orderType, pairTicker, bid.Order.Amount, bid.Order.Price);
                        orderString += "create_order(\"" + orderType + "\", " + bid.Order.Amount + ", " + bid.Order.Price + ")\n";

                        amount -= bid.Order.Amount;
                    }
                    else
                    {
                        amount = user.assetPairBalance[pairTicker].QuotedAsset / bid.Order.Price;
                        ExchangePair(user, orderType, pairTicker, amount, bid.Order.Price);
                        orderString += "create_order(\"" + orderType + "\", " + amount + ", " + bid.Order.Price + ")\n";

                        break;
                    }
                }
            }
            else
            {
                // SELL
                // 
                if (amount > user.assetPairBalance[pairTicker].BaseAsset)
                {
                    Console.WriteLine("Order amount too high! Adjusting to max. possible");
                    amount = user.assetPairBalance[pairTicker].BaseAsset;
                }

                foreach (Ask ask in orderBook.Asks)
                {
                    if (amount > ask.Order.Amount)
                    {
                        ExchangePair(user, orderType, pairTicker, ask.Order.Amount, ask.Order.Price);
                        orderString += "create_order(\"" + orderType + "\", " + ask.Order.Amount + ", " + ask.Order.Price + ")\n";

                        amount -= ask.Order.Amount;
                    }
                    else
                    {
                        ExchangePair(user, orderType, pairTicker, amount, ask.Order.Price);
                        orderString += "create_order(\"" + orderType + "\", " + amount + ", " + ask.Order.Price + ")\n";

                        break;
                    }
                }

            }
           

            //orderString += amount + ", " + orderBookType.Order.Price + ")\n";

            return orderString;

        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello Sowalab people!");

            // Init culture settings
            //CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            CultureInfo customCulture = (CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            // Init our meta-exchange
            var metaExchange = new Program();

            // Init test user and add some money to his trading account
            User testUser = new User
            {
                assetPairBalance = new Dictionary<string, AssetPairBalance>()
            };

            AssetPairBalance BTCEUR = new AssetPairBalance
            {
                BaseAsset = 1,
                QuotedAsset = 1000
            };

            testUser.assetPairBalance.Add("BTC-EUR", BTCEUR);

            Console.WriteLine("Inital user balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset), 12, MidpointRounding.ToEven)));
            Console.WriteLine("");
            Console.WriteLine("Buy 0.5 BTC");
            // Place BUY order
            int cryptoExchangeId = 0;
            OrderBookData orderBook = metaExchange.ReadOrderBookJSON(cryptoExchangeId, "../../Orderbook/order_books_data.json");
            string orderOutput = metaExchange.PlaceOrder(orderBook, testUser, "BTC-EUR", "buy", 0.5);

            Console.WriteLine(orderOutput);
            Console.WriteLine("User balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset),12,MidpointRounding.ToEven)));
            Console.WriteLine("\n");


            // Tests (will read Orderbook everytime, but that can be optimized)
            // 
            Console.WriteLine("##########\n Tests\n##########");
            
            // SELL BTC
            Console.WriteLine("Sell 1 BTC");
            // Place SELL order
            cryptoExchangeId = 1;
            orderBook = metaExchange.ReadOrderBookJSON(cryptoExchangeId, "../../Orderbook/order_books_data.json");
            orderOutput = metaExchange.PlaceOrder(orderBook, testUser, "BTC-EUR", "sell", 1);

            Console.WriteLine(orderOutput);
            Console.WriteLine("User balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset), 12, MidpointRounding.ToEven)));
            Console.WriteLine("\n");

            // SELL too much BTC
            Console.WriteLine("Sell 10 BTC");

            cryptoExchangeId = 1;
            orderBook = metaExchange.ReadOrderBookJSON(cryptoExchangeId, "../../Orderbook/order_books_data.json");
            orderOutput = metaExchange.PlaceOrder(orderBook, testUser, "BTC-EUR", "sell", 10);

            Console.WriteLine(orderOutput);
            Console.WriteLine("User balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset), 12, MidpointRounding.ToEven)));
            Console.WriteLine("\n");


            // BUY BTC
            Console.WriteLine("Buy 1 BTC");
            cryptoExchangeId = 2;
            orderBook = metaExchange.ReadOrderBookJSON(cryptoExchangeId, "../../Orderbook/order_books_data.json");
            orderOutput = metaExchange.PlaceOrder(orderBook, testUser, "BTC-EUR", "buy", 1);

            Console.WriteLine(orderOutput);
            Console.WriteLine("User balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset), 12, MidpointRounding.ToEven)));
            Console.WriteLine("\n");


            // BUY too much BTC
            Console.WriteLine("Buy 10 BTC");
            cryptoExchangeId = 2;
            orderBook = metaExchange.ReadOrderBookJSON(cryptoExchangeId, "../../Orderbook/order_books_data.json");
            orderOutput = metaExchange.PlaceOrder(orderBook, testUser, "BTC-EUR", "buy", 10);

            Console.WriteLine(orderOutput);
            Console.WriteLine("User balance: BTC " + Math.Abs(Math.Round(testUser.assetPairBalance["BTC-EUR"].BaseAsset, 12, MidpointRounding.ToEven)) + " EUR " + Math.Abs(Math.Round((testUser.assetPairBalance["BTC-EUR"].QuotedAsset), 12, MidpointRounding.ToEven)));
            Console.WriteLine("\n");

        }

    }


}
