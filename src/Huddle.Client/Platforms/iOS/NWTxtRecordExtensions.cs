using Network;
using System.Text;

namespace Huddle.Client.Platforms.iOS
{
    internal static class NWTxtRecordExtensions
    {
        public static IDictionary<string, string> ToDictionary(this NWTxtRecord? value)
        {
            var returnDictionary = new Dictionary<string, string>();

            if (value == null)
            {
                return returnDictionary;
            }

            try
            {
                AddToDictionary(value, "IpAddress", returnDictionary);
                AddToDictionary(value, "DeviceId", returnDictionary);
                AddToDictionary(value, "ServerPort", returnDictionary);
                AddToDictionary(value, "QueuePort", returnDictionary);
                AddToDictionary(value, "ListeningPort", returnDictionary);
            }
            catch { }

            return returnDictionary;
        }

        private static void AddToDictionary(NWTxtRecord value, string dictionaryKey, IDictionary<string, string> dictionary)
        {
            value.GetValue(dictionaryKey, (string? key, NWTxtRecordFindKey result, ReadOnlySpan<byte> value) =>
            {
                dictionary.Add(dictionaryKey, Encoding.ASCII.GetString(value.ToArray()));
            });
        }
    }
}
