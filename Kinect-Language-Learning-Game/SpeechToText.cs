using Microsoft.CognitiveServices.SpeechRecognition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorBasics
{

    public class SpeechToText
    {
        public class RecognizedTextArgs : EventArgs
        {
            public string Text { get; internal set; }
            public RecognizedTextArgs(string text)
            {
                this.Text = text;
            }
        }

        public delegate void TextReceivedHandler(object sender, RecognizedTextArgs args);

        private static readonly string BingSpeechSubscriptionKey = "enter key here";
        private DataRecognitionClient dataRecognitionClient;
        public static readonly string Language = "en-US";
        public event TextReceivedHandler TextReceived;

        public SpeechToText()
        {
            dataRecognitionClient = SpeechRecognitionServiceFactory.CreateDataClient(SpeechRecognitionMode.ShortPhrase, Language, BingSpeechSubscriptionKey);
            dataRecognitionClient.OnResponseReceived += DataRecognitionClient_OnResponseReceived;
            dataRecognitionClient.OnConversationError += DataRecognitionClient_OnConversationError;

            SpeechAudioFormat af = new SpeechAudioFormat();
            af.AverageBytesPerSecond = 16000 * 2;
            af.BitsPerSample = 16;
            af.ChannelCount = 1;
            af.SamplesPerSecond = 16000;
            af.EncodingFormat = AudioCompressionType.PCM;
            dataRecognitionClient.SendAudioFormat(af);
        }

        private void DataRecognitionClient_OnConversationError(object sender, SpeechErrorEventArgs e)
        {
            Console.Error.WriteLine("Error ocurred");
        }

        private void DataRecognitionClient_OnResponseReceived(object sender, SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.RecognitionStatus != RecognitionStatus.RecognitionSuccess)
            {
                return;
            }
            Console.WriteLine(e.PhraseResponse.Results[0].DisplayText);
            TextReceived?.Invoke(this, new RecognizedTextArgs(e.PhraseResponse.Results[0].DisplayText));
        }


        public void SendBytes(byte[] buffer)
        {
            try
            {
                dataRecognitionClient.SendAudio(buffer, buffer.Length);
            }
            finally
            {
                dataRecognitionClient.EndAudio();
            }
        }
    }
}