namespace Stage0;

partial class Program
{
    static void Main(string[] args)
    {
        Welcome0396();
        Welcome8991();
        Console.ReadKey();
    }

    static partial void Welcome8991();
    private static void Welcome0396()
    {
        Console.WriteLine("Enter your name: ");
        var x = Console.ReadLine()!;
        Console.WriteLine("{0}, welcome to my first console application", x);
    }
}
