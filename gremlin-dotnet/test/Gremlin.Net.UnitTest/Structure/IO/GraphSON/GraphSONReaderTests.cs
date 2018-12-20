﻿#region License

/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

#endregion

using System;
using System.Collections.Generic;
using System.Numerics;
using Gremlin.Net.Process.Traversal;
using Gremlin.Net.Structure;
using Gremlin.Net.Structure.IO.GraphSON;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gremlin.Net.UnitTest.Structure.IO.GraphSON
{
    public class GraphSONReaderTests
    {
        /// <summary>
        /// Parameters for each test supporting multiple versions of GraphSON
        /// </summary>
        public static IEnumerable<object[]> Versions => new []
        {
            new object[] { 2 },
            new object[] { 3 }
        };

        /// <summary>
        /// Parameters for each collections test supporting multiple versions of GraphSON
        /// </summary>
        public static IEnumerable<object[]> VersionsSupportingCollections => new []
        {
            new object[] { 3 }
        };

        private GraphSONReader CreateStandardGraphSONReader(int version)
        {
            if (version == 3)
            {
                return new GraphSON3Reader();
            }
            return new GraphSON2Reader();
        }

        //During CI, we encountered a case where Newtonsoft.Json version 9.0.0
        //was loaded although there is no obvious direct nor indirect dependency
        //on that version of the library. An explicit reference to version
        //11.0.0 from Gremlin.Net.UnitTest fixes that, however, it is
        //still unclear what causes the downgrade. Until resolution, we keep this test.
        [Fact]
        public void NewtonsoftJsonVersionShouldSupportReallyBigIntegers()
        {
            Assert.Equal(new Version(11, 0, 0, 0), typeof(JToken).Assembly.GetName().Version);
        }

        [Fact]
        public void ShouldDeserializeWithCustomDeserializerForNewType()
        {
            var deserializerByGraphSONType = new Dictionary<string, IGraphSONDeserializer>
            {
                {"NS:TestClass", new TestGraphSONDeserializer()}
            };
            var reader = new GraphSON2Reader(deserializerByGraphSONType);
            var graphSON = "{\"@type\":\"NS:TestClass\",\"@value\":\"test\"}";

            var jObject = JObject.Parse(graphSON);
            var readObj = reader.ToObject(jObject);

            Assert.Equal("test", readObj.Value);
        }

        [Fact]
        public void ShouldDeserializeWithCustomDeserializerForCommonType()
        {
            var customSerializerMock = new Mock<IGraphSONDeserializer>();
            var overrideTypeString = "g:Int64";
            var customSerializerByType = new Dictionary<string, IGraphSONDeserializer>
            {
                {overrideTypeString, customSerializerMock.Object}
            };
            var reader = new GraphSON2Reader(customSerializerByType);


            reader.ToObject(JObject.Parse($"{{\"@type\":\"{overrideTypeString}\",\"@value\":12}}"));

            customSerializerMock.Verify(m => m.Objectify(It.IsAny<JToken>(), It.IsAny<GraphSONReader>()));
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDateToDateTimeOffset(int version)
        {
            var graphSon = "{\"@type\":\"g:Date\",\"@value\":1475583442552}";
            var reader = CreateStandardGraphSONReader(version);

            DateTimeOffset deserializedValue = reader.ToObject(JObject.Parse(graphSon));

            var expectedDateTimeOffset = TestUtils.FromJavaTime(1475583442552);
            Assert.Equal(expectedDateTimeOffset, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDictionary(int version)
        {
            var serializedDict = "{\"age\":[{\"@type\":\"g:Int32\",\"@value\":29}],\"name\":[\"marko\"]}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedDict);
            var deserializedDict = reader.ToObject(jObject);

            var expectedDict = new Dictionary<string, dynamic>
            {
                {"age", new List<object> {29}},
                {"name", new List<object> {"marko"}}
            };
            Assert.Equal(expectedDict, deserializedDict);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeEdge(int version)
        {
            var graphSon =
                "{\"@type\":\"g:Edge\", \"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":17},\"label\":\"knows\",\"inV\":\"x\",\"outV\":\"y\",\"inVLabel\":\"xLab\",\"properties\":{\"aKey\":\"aValue\",\"bKey\":true}}}";
            var reader = CreateStandardGraphSONReader(version);

            Edge readEdge = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal((long) 17, readEdge.Id);
            Assert.Equal("knows", readEdge.Label);
            Assert.Equal(new Vertex("x", "xLabel"), readEdge.InV);
            Assert.Equal(new Vertex("y"), readEdge.OutV);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeInt(int version)
        {
            var serializedValue = "{\"@type\":\"g:Int32\",\"@value\":5}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(5, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeLong(int version)
        {
            var serializedValue = "{\"@type\":\"g:Int64\",\"@value\":5}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal((long) 5, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeFloat(int version)
        {
            var serializedValue = "{\"@type\":\"g:Float\",\"@value\":31.3}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal((float) 31.3, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDouble(int version)
        {
            var serializedValue = "{\"@type\":\"g:Double\",\"@value\":31.2}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(31.2, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeNaN(int version)
        {
            var serializedValue = "{\"@type\":\"g:Double\",\"@value\":'NaN'}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(Double.NaN, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializePositiveInfinity(int version)
        {
            var serializedValue = "{\"@type\":\"g:Double\",\"@value\":'Infinity'}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(Double.PositiveInfinity, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeNegativeInfinity(int version)
        {
            var serializedValue = "{\"@type\":\"g:Double\",\"@value\":'-Infinity'}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(Double.NegativeInfinity, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDecimal(int version)
        {
            var serializedValue = "{\"@type\":\"gx:BigDecimal\",\"@value\":-8.201}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            decimal deserializedValue = reader.ToObject(jObject);

            Assert.Equal(-8.201M, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDecimalValueAsString(int version)
        {
            var serializedValue = "{\"@type\":\"gx:BigDecimal\",\"@value\":\"7.50\"}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            decimal deserializedValue = reader.ToObject(jObject);

            Assert.Equal(7.5M, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeList(int version)
        {
            var serializedValue = "[{\"@type\":\"g:Int32\",\"@value\":5},{\"@type\":\"g:Int32\",\"@value\":6}]";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JArray.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(new List<object> {5, 6}, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeT(int version)
        {
            var graphSon = "{\"@type\":\"g:T\",\"@value\":\"label\"}";
            var reader = CreateStandardGraphSONReader(version);

            T readT = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal(T.Label, readT);
        }

        [Fact]
        public void ShouldDeserializePathFromGraphSON2()
        {
            var graphSon =
                "{\"@type\":\"g:Path\",\"@value\":{\"labels\":[[\"a\"],[\"b\",\"c\"],[]],\"objects\":[{\"@type\":\"g:Vertex\",\"@value\":{\"id\":{\"@type\":\"g:Int32\",\"@value\":1},\"label\":\"person\",\"properties\":{\"name\":[{\"@type\":\"g:VertexProperty\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":0},\"value\":\"marko\",\"label\":\"name\"}}],\"age\":[{\"@type\":\"g:VertexProperty\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":1},\"value\":{\"@type\":\"g:Int32\",\"@value\":29},\"label\":\"age\"}}]}}},{\"@type\":\"g:Vertex\",\"@value\":{\"id\":{\"@type\":\"g:Int32\",\"@value\":3},\"label\":\"software\",\"properties\":{\"name\":[{\"@type\":\"g:VertexProperty\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":4},\"value\":\"lop\",\"label\":\"name\"}}],\"lang\":[{\"@type\":\"g:VertexProperty\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":5},\"value\":\"java\",\"label\":\"lang\"}}]}}},\"lop\"]}}";
            var reader = CreateStandardGraphSONReader(2);

            Path readPath = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal("[v[1], v[3], lop]", readPath.ToString());
            Assert.Equal(new Vertex(1), readPath[0]);
            Assert.Equal(new Vertex(1), readPath["a"]);
            Assert.Equal("lop", readPath[2]);
            Assert.Equal(3, readPath.Count);
        }

        [Fact]
        public void ShouldDeserializePathFromGraphSON3()
        {
            var graphSon = "{\"@type\":\"g:Path\",\"@value\":{" +
                           "\"labels\":{\"@type\":\"g:List\",\"@value\":[{\"@type\":\"g:Set\",\"@value\":[\"z\"]}]}," +
                           "\"objects\":{\"@type\":\"g:List\",\"@value\":[{\"@type\":\"g:Vertex\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":5},\"label\":\"\"}}]}}}";
            var reader = CreateStandardGraphSONReader(3);

            Path readPath = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal("[v[5]]", readPath.ToString());
            Assert.Equal(new Vertex(5L), readPath[0]);
            Assert.Equal(new Vertex(5L), readPath["z"]);
            Assert.Equal(1, readPath.Count);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializePropertyWithEdgeElement(int version)
        {
            var graphSon =
                "{\"@type\":\"g:Property\",\"@value\":{\"key\":\"aKey\",\"value\":{\"@type\":\"g:Int64\",\"@value\":17},\"element\":{\"@type\":\"g:Edge\",\"@value\":{\"id\":{\"@type\":\"g:Int64\",\"@value\":122},\"label\":\"knows\",\"inV\":\"x\",\"outV\":\"y\",\"inVLabel\":\"xLab\"}}}}";
            var reader = CreateStandardGraphSONReader(version);

            Property readProperty = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal("aKey", readProperty.Key);
            Assert.Equal((long) 17, readProperty.Value);
            Assert.Equal(typeof(Edge), readProperty.Element.GetType());
            var edge = readProperty.Element as Edge;
            Assert.Equal((long) 122, edge.Id);
            Assert.Equal("knows", edge.Label);
            Assert.Equal("x", edge.InV.Id);
            Assert.Equal("y", edge.OutV.Id);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeTimestampToDateTimeOffset(int version)
        {
            var graphSon = "{\"@type\":\"g:Timestamp\",\"@value\":1475583442558}";
            var reader = CreateStandardGraphSONReader(version);

            DateTimeOffset deserializedValue = reader.ToObject(JObject.Parse(graphSon));

            var expectedDateTimeOffset = TestUtils.FromJavaTime(1475583442558);
            Assert.Equal(expectedDateTimeOffset, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeGuid(int version)
        {
            var graphSon = "{\"@type\":\"g:UUID\",\"@value\":\"41d2e28a-20a4-4ab0-b379-d810dede3786\"}";
            var reader = CreateStandardGraphSONReader(version);

            Guid readGuid = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal(Guid.Parse("41d2e28a-20a4-4ab0-b379-d810dede3786"), readGuid);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeVertexProperty(int version)
        {
            var graphSon =
                "{\"@type\":\"g:VertexProperty\",\"@value\":{\"id\":\"anId\",\"label\":\"aKey\",\"value\":true,\"vertex\":{\"@type\":\"g:Int32\",\"@value\":9}}}";
            var reader = CreateStandardGraphSONReader(version);

            VertexProperty readVertexProperty = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal("anId", readVertexProperty.Id);
            Assert.Equal("aKey", readVertexProperty.Label);
            Assert.True(readVertexProperty.Value);
            Assert.NotNull(readVertexProperty.Vertex);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeVertexPropertyWithLabel(int version)
        {
            var graphSon =
                "{\"@type\":\"g:VertexProperty\", \"@value\":{\"id\":{\"@type\":\"g:Int32\",\"@value\":1},\"label\":\"name\",\"value\":\"marko\"}}";
            var reader = CreateStandardGraphSONReader(version);

            VertexProperty readVertexProperty = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal(1, readVertexProperty.Id);
            Assert.Equal("name", readVertexProperty.Label);
            Assert.Equal("marko", readVertexProperty.Value);
            Assert.Null(readVertexProperty.Vertex);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeVertex(int version)
        {
            var graphSon = "{\"@type\":\"g:Vertex\", \"@value\":{\"id\":{\"@type\":\"g:Float\",\"@value\":45.23}}}";
            var reader = CreateStandardGraphSONReader(version);

            var readVertex = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal(new Vertex(45.23f), readVertex);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeVertexWithEdges(int version)
        {
            var graphSon =
                "{\"@type\":\"g:Vertex\", \"@value\":{\"id\":{\"@type\":\"g:Int32\",\"@value\":1},\"label\":\"person\",\"outE\":{\"created\":[{\"id\":{\"@type\":\"g:Int32\",\"@value\":9},\"inV\":{\"@type\":\"g:Int32\",\"@value\":3},\"properties\":{\"weight\":{\"@type\":\"g:Double\",\"@value\":0.4}}}],\"knows\":[{\"id\":{\"@type\":\"g:Int32\",\"@value\":7},\"inV\":{\"@type\":\"g:Int32\",\"@value\":2},\"properties\":{\"weight\":{\"@type\":\"g:Double\",\"@value\":0.5}}},{\"id\":{\"@type\":\"g:Int32\",\"@value\":8},\"inV\":{\"@type\":\"g:Int32\",\"@value\":4},\"properties\":{\"weight\":{\"@type\":\"g:Double\",\"@value\":1.0}}}]},\"properties\":{\"name\":[{\"id\":{\"@type\":\"g:Int64\",\"@value\":0},\"value\":\"marko\"}],\"age\":[{\"id\":{\"@type\":\"g:Int64\",\"@value\":1},\"value\":{\"@type\":\"g:Int32\",\"@value\":29}}]}}}";
            var reader = CreateStandardGraphSONReader(version);

            var readVertex = reader.ToObject(JObject.Parse(graphSon));

            Assert.Equal(new Vertex(1), readVertex);
            Assert.Equal("person", readVertex.Label);
            Assert.Equal(typeof(int), readVertex.Id.GetType());
        }

        [Theory, MemberData(nameof(VersionsSupportingCollections))]
        public void ShouldDeserializeEmptyGList(int version)
        {
            var graphSon =
                "{\"@type\":\"g:List\", \"@value\": []}";
            var reader = CreateStandardGraphSONReader(version);

            var deserializedValue = reader.ToObject(JObject.Parse(graphSon));
            Assert.Equal(new object[0], deserializedValue);
        }

        [Theory, MemberData(nameof(VersionsSupportingCollections))]
        public void ShouldDeserializeGList(int version)
        {
            const string json = "{\"@type\":\"g:List\", \"@value\": [{\"@type\": \"g:Int32\", \"@value\": 1}," +
                                "{\"@type\": \"g:Int32\", \"@value\": 2}, {\"@type\": \"g:Int32\", \"@value\": 3}]}";
            var reader = CreateStandardGraphSONReader(version);

            var deserializedValue = reader.ToObject(JObject.Parse(json));

            Assert.Equal((IList<object>)new object[] { 1, 2, 3}, deserializedValue);
        }

        [Theory, MemberData(nameof(VersionsSupportingCollections))]
        public void ShouldDeserializeGSet(int version)
        {
            const string json = "{\"@type\":\"g:Set\", \"@value\": [{\"@type\": \"g:Int32\", \"@value\": 1}," +
                                "{\"@type\": \"g:Int32\", \"@value\": 2}, {\"@type\": \"g:Int32\", \"@value\": 3}]}";
            var reader = CreateStandardGraphSONReader(version);

            var deserializedValue = reader.ToObject(JObject.Parse(json));

            Assert.Equal((ISet<object>)new HashSet<object>{ 1, 2, 3}, deserializedValue);
        }

        [Theory, MemberData(nameof(VersionsSupportingCollections))]
        public void ShouldDeserializeGMap(int version)
        {
            const string json = "{\"@type\":\"g:Map\", \"@value\": [\"a\",{\"@type\": \"g:Int32\", \"@value\": 1}, " +
                                "\"b\", {\"@type\": \"g:Int32\", \"@value\": 2}]}";
            var reader = CreateStandardGraphSONReader(version);

            var deserializedValue = reader.ToObject(JObject.Parse(json));

            Assert.Equal(new Dictionary<object, object>{ { "a", 1 }, { "b", 2 }}, deserializedValue);
        }

        [Fact]
        public void ShouldDeserializeTraverser()
        {
            dynamic d = JObject.Parse("{\"@type\":\"g:Traverser\",\"@value\":1}");

            Assert.NotNull(d);
            Assert.Equal("g:Traverser", (string)d["@type"]);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeDurationToTimeSpan(int version)
        {
            var serializedValue = "{\"@type\":\"gx:Duration\",\"@value\":\"PT120H\"}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            TimeSpan deserializedValue = reader.ToObject(jObject);

            Assert.Equal(TimeSpan.FromDays(5), deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeBigInteger(int version)
        {
            var serializedValue = "{\"@type\":\"gx:BigInteger\",\"@value\":123456789}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            BigInteger deserializedValue = reader.ToObject(jObject);

            Assert.Equal(BigInteger.Parse("123456789"), deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeBigIntegerValueAsString(int version)
        {
            var serializedValue = "{\"@type\":\"gx:BigInteger\",\"@value\":\"123456789\"}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            BigInteger deserializedValue = reader.ToObject(jObject);

            Assert.Equal(BigInteger.Parse("123456789"), deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeReallyBigIntegerValue(int version)
        {
            var serializedValue = "{\"@type\":\"gx:BigInteger\",\"@value\":123456789987654321123456789987654321}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            BigInteger deserializedValue = reader.ToObject(jObject);

            Assert.Equal(BigInteger.Parse("123456789987654321123456789987654321"), deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeByte(int version)
        {
            var serializedValue = "{\"@type\":\"gx:Byte\",\"@value\":1}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(1, deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeByteBuffer(int version)
        {
            var serializedValue = "{\"@type\":\"gx:ByteBuffer\",\"@value\":\"c29tZSBieXRlcyBmb3IgeW91\"}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(Convert.FromBase64String("c29tZSBieXRlcyBmb3IgeW91"), deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeChar(int version)
        {
            var serializedValue = "{\"@type\":\"gx:Char\",\"@value\":\"x\"}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal('x', deserializedValue);
        }

        [Theory, MemberData(nameof(Versions))]
        public void ShouldDeserializeInt16(int version)
        {
            var serializedValue = "{\"@type\":\"gx:Int16\",\"@value\":100}";
            var reader = CreateStandardGraphSONReader(version);

            var jObject = JObject.Parse(serializedValue);
            var deserializedValue = reader.ToObject(jObject);

            Assert.Equal(100, deserializedValue);
        }
    }

    internal class TestGraphSONDeserializer : IGraphSONDeserializer
    {
        public dynamic Objectify(JToken graphsonObject, GraphSONReader reader)
        {
            return new TestClass {Value = graphsonObject.ToString()};
        }
    }
}