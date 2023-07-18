using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using Azure.DigitalTwins.Core;
using Telstra.Twins.Attributes;
using Telstra.Twins.Common;
using Telstra.Twins.Helpers;

namespace Telstra.Twins
{
    public abstract partial class TwinBase : BasicDigitalTwin
    {
        private string _modelId;

        protected TwinBase()
        {
            // Read any information provided using the DigitalTwinAttribute.
            // This is results in the attributes for the type being cached.
            this.ReadAttributeInfo();
        }

        /// <summary>
        /// Compiles a flat list of a twin and its related twins
        /// Conveniently (not by design) the twins are returned in order of dependency
        /// </summary>
        /// <returns></returns>
        public List<TwinBase> Flatten()
        {
            List<TwinBase> list = new List<TwinBase> { this };
            list.AddRange(TraverseTwin(this));
            return list.Distinct().ToList();
        }

        public List<Type> GetDependentTypes()
        {
            var modelTypes = this.Flatten()
                .Select(t => t.GetType())
                .ToList();

            var derivedTypes = new List<Type>();
            modelTypes.ForEach(m =>
            {
                derivedTypes.AddRange(GetInheritance(m));
                derivedTypes.AddRange(m.GetModelComponents().Select(c => c.PropertyType.GetModelPropertyType()));
            });

            modelTypes.AddRange(derivedTypes);
            modelTypes = modelTypes.Distinct()
                .OrderBy(t => t, TypeDerivationComparer.Instance)
                .ToList();

            return modelTypes;
        }

        private List<Type> GetInheritance([NotNull] Type t)
        {
            var types = new List<Type>();

            if (t.BaseType != null && t.BaseType != typeof(TwinBase))
            {
                types.AddRange(GetInheritance(t.BaseType, types));
                types.Add(t.BaseType);
            }

            return types;
        }

        private List<Type> GetInheritance([NotNull] Type t, [NotNull] List<Type> types)
        {
            if (t.BaseType != null && t.BaseType != typeof(TwinBase))
            {
                types.AddRange(GetInheritance(t.BaseType, types));
                types.Add(t.BaseType);
            }

            return types;
        }

        public List<BasicRelationship> GetRelationships()
        {
            var relationships = this.GetType().GetTwinRelationships();
            var result = new List<BasicRelationship>();

            relationships.ForEach(r =>
            {
                var prop = r.GetValue(this);
                var relationshipType = r.Name.ToCamelCase();
                if (prop is IEnumerable<TwinBase> twinBaseObjects)
                {
                    twinBaseObjects.ToList().ForEach(r =>
                    {
                        result.Add(new BasicRelationship { SourceId = this.Id, TargetId = r.Id, Name = relationshipType });
                    });
                }
                else if (prop is TwinBase twinProp)
                {
                    result.Add(new BasicRelationship { SourceId = this.Id, TargetId = twinProp.Id, Name = relationshipType });
                }
            });
            return result;
        }

        private List<TwinBase> TraverseTwin(TwinBase twin)
        {
            var list = new List<TwinBase>();
            twin.GetType()
                .GetTwinRelationships()
                .ForEach(p =>
                {
                    var prop = p.GetValue(twin);
                    if (prop is IEnumerable<TwinBase>)
                    {
                        var relationships = prop as IEnumerable<TwinBase>;
                        relationships.ToList().ForEach(r =>
                        {
                            list.Add(r);
                            list.AddRange(TraverseTwin(r));
                        });
                    }
                    else
                    {
                        if (prop is TwinBase twinProp)
                        {
                            list.Add(twinProp);
                            list.AddRange(TraverseTwin(twinProp));
                        }
                    }
                });

            return list;
        }

        [TwinModelOnlyProperty("@id")]
        public string ModelId
        {
            get => _modelId;
            set
            {
                _modelId = value;
                var twinMetadata = this.Metadata;
                if (twinMetadata != null)
                {
                    twinMetadata.ModelId = ModelId;
                }
            }
        }

        [TwinModelOnlyProperty("@type")]
        public string ModelType { get; set; } = "Interface";

        [TwinModelOnlyProperty("extends")]
        public string ExtendsModelId { get; set; }

        [TwinModelOnlyProperty("@context")]
        public string Context { get; set; } = "dtmi:dtdl:context;2";

        [TwinModelOnlyProperty("displayName")]
        protected string DisplayName { get; set; }

        private void ReadAttributeInfo()
        {
            var type = this.GetType();
            if (type.TryGetAttribute<DigitalTwinAttribute>(out var twinAttribute))
            {
                DisplayName = twinAttribute.DisplayName;
                ModelId = twinAttribute.GetFullModelId(type);
                ModelType = twinAttribute.ModelType;

                // Get the model Id that this model is extending.
                if (twinAttribute.ExtendsModelId != null)
                {
                    ExtendsModelId = twinAttribute.ExtendsModelId;
                }
                else if (type.BaseType != null &&
                         type.BaseType.TryGetAttribute<DigitalTwinAttribute>(out var baseTwinAttribute))
                {
                    ExtendsModelId = baseTwinAttribute.GetFullModelId(type.BaseType);
                }
            }
        }

        public void RefreshContents()
        {
            var properties = this.GetType().GetTwinProperties();

            Contents = properties.Select(p => (key: p.Name.ToCamelCase(), value: p.GetValue(this)))
                    .ToDictionary(c => c.key,
                        c => c.value is TwinBase twinBase ?
                            twinBase.ToTwinComponent() : c.value);
            Contents = CleanupDigitalTwinContents(Contents);
        }

        public BasicDigitalTwinComponent ToTwinComponent()
        {
            var properties = this.GetType().GetTwinProperties();

            var basicTwinComponent = new BasicDigitalTwinComponent()
            {
                Contents = properties.Select(p => (key: p.Name.ToCamelCase(), value: p.GetValue(this)))
                    .ToDictionary(c => c.key,
                        c => c.value is TwinBase twinBase ?
                            twinBase.ToTwinComponent() : c.value)
            };

            return basicTwinComponent;
        }


        private IDictionary<string, object> CleanupDigitalTwinContents(IDictionary<string, object> twinDataContents)
        {
            _ = twinDataContents ?? throw new ArgumentNullException(nameof(twinDataContents));

            // removing the null values from the list
            twinDataContents = twinDataContents
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value);

            foreach (var kv in twinDataContents
                .Where(x => x.Value.GetType().IsClass
                && x.Value.GetType()?.FullName?.StartsWith("System.", StringComparison.InvariantCultureIgnoreCase) == false))
            {
                twinDataContents[kv.Key] = RemoveNullPropertiesFromObject(kv.Value);
            }
            return twinDataContents;
        }

        private object RemoveNullPropertiesFromObject(object objectToTransform)
        {
            var type = objectToTransform.GetType();
            var returnClass = new ExpandoObject() as IDictionary<string, object>;
            foreach (var propertyInfo in type.GetProperties())
            {
                var value = propertyInfo.GetValue(objectToTransform);
                if (value is not null)
                {
                    returnClass.Add(propertyInfo.Name.ToCamelCase(), value);
                }
            }
            return returnClass;
        }
    }
}
