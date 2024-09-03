﻿namespace IronworksTranslator.Models
{
    public class BiggsBody
    {
        public string input_sentence { get; set; }
        public string input_language {  get; set; }
        public string output_sentence { get; set; }

        public string output_language { get; set; }
        public DateTime timestamp { get; set; }
        public string comment { get; set; }
    }
}