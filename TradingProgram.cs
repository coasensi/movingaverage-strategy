using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

class TradingProgram
{
    static async Task Main()
    {
        Console.WriteLine("trading program - moving average crossover strategy");

        // input api key and symbol
        Console.Write("alpha vantage api key: ");
        string apiKey = Console.ReadLine();

        Console.Write("enter ticker: ");
        string symbol = Console.ReadLine();

        // input short-term and long-term periods for moving averages
        Console.Write("enter short-term period for moving average: ");
        int shortTermPeriod = Convert.ToInt32(Console.ReadLine());

        Console.Write("enter long-term period for moving average: ");
        int longTermPeriod = Convert.ToInt32(Console.ReadLine());

        // validate periods
        if (shortTermPeriod <= 0 || longTermPeriod <= 0 || shortTermPeriod >= longTermPeriod)
        {
            Console.WriteLine("invalid periods. please enter positive integers where short-term period is less than long-term period.");
            return;
        }

        // fetch historical prices
        List<double> prices = await FetchHistoricalPrices(apiKey, symbol);

        if (prices == null || prices.Count == 0)
        {
            Console.WriteLine("failed to fetch historical prices.");
            return;
        }

        // calculate moving averages
        List<double> shortTermMA = CalculateMovingAverage(prices, shortTermPeriod);
        List<double> longTermMA = CalculateMovingAverage(prices, longTermPeriod);

        // generate trading signals
        List<string> signals = GenerateSignals(shortTermMA, longTermMA);

        // backtest strategy
        double finalPortfolioValue = Backtest(prices, signals);

        // output results
        for (int i = 0; i < prices.Count; i++)
        {
            string shortTermMAValue = i >= shortTermPeriod - 1 ? shortTermMA[i - (shortTermPeriod - 1)].ToString("F2") : "N/A";
            string longTermMAValue = i >= longTermPeriod - 1 ? longTermMA[i - (longTermPeriod - 1)].ToString("F2") : "N/A";
            Console.WriteLine($"price: {prices[i]}, short ma: {shortTermMAValue}, long ma: {longTermMAValue}, signal: {signals[i]}");
        }

        Console.WriteLine($"final portfolio value: {finalPortfolioValue:C2}");
    }

    static async Task<List<double>> FetchHistoricalPrices(string apiKey, string symbol)
    {
        string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={apiKey}&outputsize=full";

        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(jsonResponse);
                var prices = data["Time Series (Daily)"]?
                    .Values<JObject>()
                    .Select(x => (double)x["4. close"])
                    .Reverse()
                    .ToList();

                return prices;
            }
        }

        return null;
    }

    static List<double> CalculateMovingAverage(List<double> prices, int period)
    {
        List<double> movingAverage = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            if (i < period - 1)
            {
                movingAverage.Add(double.NaN); // not enough data to calculate moving average
            }
            else
            {
                double sum = 0;
                for (int j = 0; j < period; j++)
                {
                    sum += prices[i - j];
                }
                movingAverage.Add(sum / period);
            }
        }

        return movingAverage;
    }

    static List<string> GenerateSignals(List<double> shortTermMA, List<double> longTermMA)
    {
        List<string> signals = new List<string>();

        for (int i = 0; i < shortTermMA.Count; i++)
        {
            if (double.IsNaN(shortTermMA[i]) || double.IsNaN(longTermMA[i]))
            {
                signals.Add("N/A");
            }
            else if (shortTermMA[i] > longTermMA[i] && (i == 0 || shortTermMA[i - 1] <= longTermMA[i - 1]))
            {
                signals.Add("buy");
            }
            else if (shortTermMA[i] < longTermMA[i] && (i == 0 || shortTermMA[i - 1] >= longTermMA[i - 1]))
            {
                signals.Add("sell");
            }
            else
            {
                signals.Add("hold");
            }
        }

        return signals;
    }

    static double Backtest(List<double> prices, List<string> signals)
    {
        double portfolioValue = 10000; // initial portfolio value
        double sharesOwned = 0;

        for (int i = 0; i < prices.Count; i++)
        {
            if (signals[i] == "buy" && portfolioValue > 0)
            {
                sharesOwned = portfolioValue / prices[i];
                portfolioValue = 0;
            }
            else if (signals[i] == "sell" && sharesOwned > 0)
            {
                portfolioValue = sharesOwned * prices[i];
                sharesOwned = 0;
            }
        }

        // if we still own shares at the end, sell them at the last price
        if (sharesOwned > 0)
        {
            portfolioValue = sharesOwned * prices.Last();
        }

        return portfolioValue;
    }
}
