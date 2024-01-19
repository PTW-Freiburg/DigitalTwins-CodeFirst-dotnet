#nullable enable
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using FluentAssertions;
using KellermanSoftware.CompareNetObjects;
using Telstra.Twins.Core;
using Telstra.Twins.Helpers;
using Telstra.Twins.Models;
using Telstra.Twins.Services;
using Xunit;
using Xunit.Abstractions;

namespace Telstra.Twins.Test
{
    public class SerializationTests
    {
        protected ITestOutputHelper TestOutput { get; }
        protected DigitalTwinSerializer Serializer { get; }

        public SerializationTests(ITestOutputHelper testOutputHelper)
        {
            var modelLibrary = new ModelLibrary();
            Serializer = new DigitalTwinSerializer(modelLibrary);
            TestOutput = testOutputHelper;
        }

        [Theory]
        [MemberData(nameof(ModelTestData))]
        public void ShouldSerialiseModelToDTDL(string expectedModel, Type twinType)
        {
            var model = Serializer.SerializeModel(twinType);
            JsonAssert.Equal(expectedModel, model);
        }

        [Theory]
        [InlineData(typeof(Building))]
        [InlineData(typeof(Floor))]
        [InlineData(typeof(TwinWithNestedObject))]
        [InlineData(typeof(SimpleTwin))]
        [InlineData(typeof(TwinWithAllAttributes))]
        [InlineData(typeof(TwinWithEnum))]
        [InlineData(typeof(TwinWithProtectedCtor))]
        public void ShouldSerialiseModelToDTDLCustomized(Type type)
        {
            var model = Serializer.SerializeModel(type);
            model.Should().NotBeNull();

            if (!Directory.Exists("DTDL Models"))
            {
                Directory.CreateDirectory("DTDL Models");
            }
            File.WriteAllText(Path.Combine("DTDL Models", $"{type.Name}.json"), model);
        }

        [Fact]
        public void Building_RelationName_Should_MatchWithAttribute()
        {
            var model = typeof(Building).GetModelRelationships();
            var moelRel = ModelRelationship.Create(model[0]);
            Assert.Equal("contains", moelRel.Name);
        }

        [Theory]
        [MemberData(nameof(TwinTestData))]
        public void ShouldSerializeTwinToDTDL(string twinDTDL, object twinObject)
        {
            var expectedDTDL = Serializer.SerializeTwin(twinObject);
            JsonAssert.Equal(twinDTDL, expectedDTDL);
        }


        [Theory]
        [MemberData(nameof(TwinTestData))]
        public void ShouldDeserializeTwinToObject(string twinDTDL, object twinObject)
        {
            var deserializedObject = Serializer.DeserializeTwin(twinDTDL);
            deserializedObject.ShouldCompare(twinObject);
        }


        [Fact]
        public void RefreshContents_SimpleTwin_Should_Work()
        {
            // arrange
            var simpleTwin = new SimpleTwin()
            {
                Id = "122233",
                ETag = new Azure.ETag("4444"),
                Quantity = 1,
                Measurement = 2
            };

            // act
            simpleTwin.ToBasicTwin();

            // assert
            simpleTwin.Contents.Count.Should().Be(1);
        }

        [Fact]
        public void RefreshContents_TwinWithAllAttributes_Should_Work()
        {
            // arrange
            var twinWithAllAttributes = new TwinWithAllAttributes()
            {
                Id = "0",
                ETag = new Azure.ETag("0"),
                Property = "property",
                Flag = true,
                ComponentTwin = new SimpleTwin() { Quantity = 1, Measurement = 2 },
                IntArray = new List<int>() { 1, 2, 3 },
                StringMap = new Dictionary<string, string>()
                {
                    {"key","value"}
                },
                GuidId = Guid.Parse("b2d1ab5e-d953-4003-85e1-1018a00fe848"),
                NullableId = null
            };

            // act
            var basicTwin = twinWithAllAttributes.ToBasicTwin();

            twinWithAllAttributes.Contents.Count.Should().Be(5);
            basicTwin.Id.Should().Be(twinWithAllAttributes.Id);
            basicTwin.ETag.Should().Be(twinWithAllAttributes.ETag);
            basicTwin.Contents.Count.Should().Be(5);
        }

        [Fact]
        public void RefreshContents_TwinWithNestedObject_Should_Work()
        {
            // arrange
            var twinWithNestedObject = new TwinWithNestedObject()
            {
                Id = "11111", // model property will be omitted
                ETag = new Azure.ETag("abcd"), // model property will be omitted
                NestedObj = new NestedObject()
                {
                    Name = "name",
                    Value = null,
                    State = State.Inactive
                },
                Speed = 50 // telemetry will be omitted
            };

            // act
            var basicTwin = twinWithNestedObject.ToBasicTwin();

            // assert
            twinWithNestedObject.Contents.Count.Should().Be(1);
            var nestedComponent = twinWithNestedObject.Contents["nestedObj"] as ExpandoObject;
            nestedComponent.Should().NotBeNull();
            int count = ((IDictionary<string, object>)nestedComponent!).Count;
            count.Should().Be(2);

            basicTwin.Id.Should().Be(twinWithNestedObject.Id);
            basicTwin.ETag.Should().Be(twinWithNestedObject.ETag);

            basicTwin.Contents.Count.Should().Be(1);
            var nestedComponent2 = twinWithNestedObject.Contents["nestedObj"] as ExpandoObject;
            nestedComponent2.Should().NotBeNull();
            int count2 = ((IDictionary<string, object>)nestedComponent2!).Count;
            count2.Should().Be(2);
        }

        public static IEnumerable<object[]> ModelTestData()
        {
            yield return new object[] {
                DataGenerator.SimpleTwinModel,
                DataGenerator.simpleTwin.GetType()
            };
            yield return new object[] {
                DataGenerator.TwinWithAllAttributesModel,
                DataGenerator.twinWithAllAttributes.GetType()
            };
            yield return new object[] {
                DataGenerator.TwinWithNestedObjectModel,
                DataGenerator.twinWithNestedObject.GetType()
            };
            yield return new object[] {
                DataGenerator.TwinWithRelationshipModel,
                DataGenerator.twinWithRelationship.GetType()
            };
            yield return new object[] {
                DataGenerator.TwinWithMinMultiplicityModel,
                typeof(TwinWithMinMultiplicity)
            };
            yield return new object[] {
                DataGenerator.TwinWithEnumModel,
                typeof(TwinWithEnum)
            };
            yield return new object[] {
                DataGenerator.TwinWithReadOnlyPropertiesModel,
                typeof(TwinWithReadOnlyProperties)
            };
        }

        public static IEnumerable<object[]> TwinTestData()
        {
            yield return new object[] {
                DataGenerator.SimpleTwinDTDL,
                DataGenerator.simpleTwin
            };
            yield return new object[] {
                DataGenerator.TwinWithAllAttributesDTDL,
                DataGenerator.twinWithAllAttributes
            };
            yield return new object[] {
                DataGenerator.TwinWithNestedObjectDTDL,
                DataGenerator.twinWithNestedObject
            };
            yield return new object[] {
                DataGenerator.TwinWithRelationshipDTDL,
                DataGenerator.twinWithRelationship
            };
            yield return new object[] {
                DataGenerator.TwinWithEnumDTDL,
                DataGenerator.twinWithEnum
            };
            yield return new object[] {
                DataGenerator.TwinWithReadOnlyPropertiesDTDL,
                DataGenerator.twinWithReadOnlyProperties
            };
        }
    }
}
