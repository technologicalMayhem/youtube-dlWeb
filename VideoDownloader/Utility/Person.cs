using System;

namespace VideoDownloader.Utility
{
    public class Person
    {
        public string Name;
        public string Surname;

        public int Age
        {
            get => _age;
            set => _age = value;
        }

        public DateTime BirthDate;
        private int _age;
    }
}