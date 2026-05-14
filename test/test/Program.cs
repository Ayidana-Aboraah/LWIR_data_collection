using System;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            int checker = Convert.ToInt32(Console.ReadLine());

            string result = (checker % 2 == 0) ? "Even" : "Odd";
            Console.WriteLine($"{checker} is an {result} number");
        }
    }
}