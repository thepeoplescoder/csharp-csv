// CSV test.

using System;
using System.Text;
using CSV;

class CSVtest
{

    public static void Main(string[] args)
    {
        CSVWriter cw;
        CSVReader cr;

        cw = new CSVWriter("test.csv");
        cw.Write("Sample text");
        cw.Write(23424);
        cw.Write("This is a string\"that contains a double quote character.");
        cw.Close();

        cr = new CSVReader("test.csv");
        object x;
        while ((x = cr.ReadNext()) != null)
        {
            Console.WriteLine(x);
        }
        cr.Close();
    }
}
