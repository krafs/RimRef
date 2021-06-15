using System;

namespace StripAndUploadRimRef
{
    public interface ILog
    {
        void Log(string text);
    }

    public class VoidLogger : ILog
    {
        public void Log(string text)
        {
        }
    }

    public class Logger : ILog
    {
        public void Log(string text)
        {
            Console.WriteLine(text);
        }
    }
}