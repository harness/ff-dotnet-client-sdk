/*
 * Harness feature flag service client apis
 *
 * No description provided (generated by Openapi Generator https://github.com/openapitools/openapi-generator)
 *
 * The version of the OpenAPI document: 1.0.0
 * Contact: cf@harness.io
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using FileParameter = io.harness.ff_dotnet_client_sdk.openapi.Client.FileParameter;
using OpenAPIDateConverter = io.harness.ff_dotnet_client_sdk.openapi.Client.OpenAPIDateConverter;

namespace io.harness.ff_dotnet_client_sdk.openapi.Model
{
    /// <summary>
    /// Target map provides the details of a target that belongs to a flag
    /// </summary>
    [DataContract(Name = "TargetMap")]
    internal partial class TargetMap : IEquatable<TargetMap>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TargetMap" /> class.
        /// </summary>
        [JsonConstructorAttribute]
        protected TargetMap() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="TargetMap" /> class.
        /// </summary>
        /// <param name="identifier">The identifier for the target (required).</param>
        /// <param name="name">The name of the target (required).</param>
        public TargetMap(string identifier = default(string), string name = default(string))
        {
            // to ensure "identifier" is required (not null)
            if (identifier == null)
            {
                throw new ArgumentNullException("identifier is a required property for TargetMap and cannot be null");
            }
            this.Identifier = identifier;
            // to ensure "name" is required (not null)
            if (name == null)
            {
                throw new ArgumentNullException("name is a required property for TargetMap and cannot be null");
            }
            this.Name = name;
        }

        /// <summary>
        /// The identifier for the target
        /// </summary>
        /// <value>The identifier for the target</value>
        [DataMember(Name = "identifier", IsRequired = true, EmitDefaultValue = true)]
        public string Identifier { get; set; }

        /// <summary>
        /// The name of the target
        /// </summary>
        /// <value>The name of the target</value>
        [DataMember(Name = "name", IsRequired = true, EmitDefaultValue = true)]
        public string Name { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class TargetMap {\n");
            sb.Append("  Identifier: ").Append(Identifier).Append("\n");
            sb.Append("  Name: ").Append(Name).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as TargetMap);
        }

        /// <summary>
        /// Returns true if TargetMap instances are equal
        /// </summary>
        /// <param name="input">Instance of TargetMap to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(TargetMap input)
        {
            if (input == null)
            {
                return false;
            }
            return 
                (
                    this.Identifier == input.Identifier ||
                    (this.Identifier != null &&
                    this.Identifier.Equals(input.Identifier))
                ) && 
                (
                    this.Name == input.Name ||
                    (this.Name != null &&
                    this.Name.Equals(input.Name))
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.Identifier != null)
                {
                    hashCode = (hashCode * 59) + this.Identifier.GetHashCode();
                }
                if (this.Name != null)
                {
                    hashCode = (hashCode * 59) + this.Name.GetHashCode();
                }
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

}