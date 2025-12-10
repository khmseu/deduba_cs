namespace Interfaces
{
    public interface ILogging
    {
        void Info(string msg);
        void Error(string msg);
        void Debug(string msg);
    }
}
