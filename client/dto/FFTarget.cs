using System.Collections.Generic;
using System.Linq;

namespace io.harness.ff_dotnet_client_sdk.client.dto
{
    public class FFTarget
    {
        public static TargetBuilder Builder()
        {
            return new TargetBuilder();
        }
        
        public FFTarget(string identifier, string name, Dictionary<string, string>? attributes = null)
        {
            Attributes = attributes ?? new Dictionary<string, string>();
            Identifier = identifier;
            Name = name;
        }
        
        public string Name { get; }
        public string Identifier { get; }
        public Dictionary<string, string> Attributes { get; }
        
        public override string ToString()
        {
            var attributesStr = string.Join(", ", Attributes.Select(kv => $"{kv.Key}: {kv.Value}"));
            return $"Identifier: {Identifier}, Name: {Name}, Attributes: {attributesStr}".TrimEnd(',', ' ');
        }


        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Identifier);
        }
        
        
        public override bool Equals(object obj)
        {
            if (obj is FFTarget other)
            {
                return Identifier == other.Identifier && AreDictionariesEqual(Attributes, other.Attributes);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            // Overflow is fine, just wrap
            unchecked 
            {
                int hash = 17;

                // Hash code for Identifier
                hash = hash * 31 + (Identifier != null ? Identifier.GetHashCode() : 0);

                // Combine hash codes for each key-value pair in the dictionary
                foreach (var pair in Attributes)
                {
                    hash = hash * 31 + (pair.Key != null ? pair.Key.GetHashCode() : 0);
                    hash = hash * 31 + (pair.Value != null ? pair.Value.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private static bool AreDictionariesEqual(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            if (dict1.Count != dict2.Count) 
                return false;

            foreach (var pair in dict1)
            {
                if (!dict2.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    return false;
            }
            return true;
        }
    }

    public class TargetBuilder
    {
        private string _identifier = "";
        private string _name = "";
        private Dictionary<string, string> _attributes = new();

        public TargetBuilder()
        {
        }

        public TargetBuilder Identifier(string identifier)
        {
            _identifier = identifier;
            return this;
        }
        
        public TargetBuilder Name(string name)
        {
            _name = name;
            return this;
        }
        
        public TargetBuilder Attributes(Dictionary<string, string> attributes)
        {
            _attributes = attributes;
            return this;
        }

        public FFTarget Build()
        {
            return new FFTarget(_identifier, _name, _attributes);
        }
    }
}
