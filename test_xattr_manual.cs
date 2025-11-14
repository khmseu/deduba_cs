using System;
using System.Text.Json;
using OsCalls;

class Program
{
    static void Main()
    {
        var testFile = "/tmp/test_xattr_file.txt";
        
        Console.WriteLine("=== Testing Xattr.ListXattr ===");
        try
        {
            var xattrList = Xattr.ListXattr(testFile);
            Console.WriteLine("Result: " + xattrList.ToJsonString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        
        Console.WriteLine("\n=== Testing Xattr.GetXattr ===");
        try
        {
            var value = Xattr.GetXattr(testFile, "user.test_attr");
            Console.WriteLine("Result: " + value.ToJsonString());
            Console.WriteLine("Value: " + value["value"]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        
        Console.WriteLine("\n=== Testing with another attribute ===");
        try
        {
            var value = Xattr.GetXattr(testFile, "user.description");
            Console.WriteLine("Result: " + value.ToJsonString());
            Console.WriteLine("Value: " + value["value"]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
