package com.tinkerpop.gremlin.structure.io.graphml;

import com.tinkerpop.gremlin.structure.Direction;
import com.tinkerpop.gremlin.structure.Edge;
import com.tinkerpop.gremlin.structure.Element;
import com.tinkerpop.gremlin.structure.Graph;
import com.tinkerpop.gremlin.structure.Vertex;
import com.tinkerpop.gremlin.structure.io.GraphReader;
import com.tinkerpop.gremlin.structure.util.batch.BatchGraph;
import com.tinkerpop.gremlin.util.function.QuintFunction;
import com.tinkerpop.gremlin.util.function.TriFunction;

import javax.xml.stream.XMLInputFactory;
import javax.xml.stream.XMLStreamException;
import javax.xml.stream.XMLStreamReader;
import javax.xml.stream.events.XMLEvent;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;

/**
 * GraphMLReader writes the data from a GraphML stream to a graph.  Note that this format is lossy, in the sense that data
 * types and features of Gremlin Structure not supported by GraphML are not serialized.  This format is meant for
 * external export of a graph to tools outside of Gremlin Structure graphs.
 *
 * @author Marko A. Rodriguez (http://markorodriguez.com)
 * @author Alex Averbuch (alex.averbuch@gmail.com)
 * @author Joshua Shinavier (http://fortytwo.net)
 * @author Stephen Mallette (http://stephen.genoprime.com)
 */
public class GraphMLReader implements GraphReader {
    private final XMLInputFactory inputFactory = XMLInputFactory.newInstance();

    private final String vertexIdKey;
    private final String edgeIdKey;
    private final String edgeLabelKey;
    private final String vertexLabelKey;
    private final long batchSize;
    private final boolean incrementalLoading;

    private GraphMLReader(final String vertexIdKey, final String edgeIdKey,
                          final String edgeLabelKey, final String vertexLabelKey,
                          final long batchSize, final boolean incrementalLoading) {
        this.vertexIdKey = vertexIdKey;
        this.edgeIdKey = edgeIdKey;
        this.edgeLabelKey = edgeLabelKey;
        this.batchSize = batchSize;
        this.vertexLabelKey = vertexLabelKey;
        this.incrementalLoading = !incrementalLoading;
    }

    // todo: graphml has classic graph with string ids - can now support numerics cleanly

    @Override
    public Vertex readVertex(final InputStream inputStream, final Direction direction,
                             final TriFunction<Object, String, Object[], Vertex> vertexMaker,
                             final QuintFunction<Object, Object, Object, String, Object[], Edge> edgeMaker) throws IOException {
        throw new UnsupportedOperationException("GraphML does not allow for a partial structure");
    }

    @Override
    public Vertex readVertex(final InputStream inputStream, final TriFunction<Object, String, Object[], Vertex> vertexMaker) throws IOException {
        throw new UnsupportedOperationException("GraphML does not allow for a partial structure");
    }

    @Override
    public Edge readEdge(final InputStream inputStream, final QuintFunction<Object, Object, Object, String, Object[], Edge> edgeMaker) throws IOException {
        throw new UnsupportedOperationException("GraphML does not allow for a partial structure");
    }

    @Override
    public void readGraph(final InputStream graphInputStream,final Graph graphToWriteTo) throws IOException {
        try {
            final XMLStreamReader reader = inputFactory.createXMLStreamReader(graphInputStream);

            // will throw an exception if not constructed properly
            final BatchGraph graph = new BatchGraph.Builder<>(graphToWriteTo)
                    .vertexIdKey(vertexIdKey)
                    .loadFromScratch(incrementalLoading)
                    .bufferSize(batchSize).build();

            final Map<String, String> keyIdMap = new HashMap<>();
            final Map<String, String> keyTypesMaps = new HashMap<>();

            // Buffered Vertex Data
            String vertexId = null;
            String vertexLabel = null;
            Map<String, Object> vertexProps = null;
            boolean isInVertex = false;

            // Buffered Edge Data
            String edgeId = null;
            String edgeLabel = null;
            Vertex edgeInVertex = null;
            Vertex edgeOutVertex = null;
            Map<String, Object> edgeProps = null;
            boolean isInEdge = false;

            while (reader.hasNext()) {
                final Integer eventType = reader.next();
                if (eventType.equals(XMLEvent.START_ELEMENT)) {
                    final String elementName = reader.getName().getLocalPart();

                    switch (elementName) {
                        case GraphMLTokens.KEY:
                            final String id = reader.getAttributeValue(null, GraphMLTokens.ID);
                            final String attributeName = reader.getAttributeValue(null, GraphMLTokens.ATTR_NAME);
                            final String attributeType = reader.getAttributeValue(null, GraphMLTokens.ATTR_TYPE);
                            keyIdMap.put(id, attributeName);
                            keyTypesMaps.put(id, attributeType);
                            break;
                        case GraphMLTokens.NODE:
                            vertexId = reader.getAttributeValue(null, GraphMLTokens.ID);
                            isInVertex = true;
                            vertexProps = new HashMap<>();
                            break;
                        case GraphMLTokens.EDGE:
                            edgeId = reader.getAttributeValue(null, GraphMLTokens.ID);

                            final String vertexIdOut = reader.getAttributeValue(null, GraphMLTokens.SOURCE);
                            final String vertexIdIn = reader.getAttributeValue(null, GraphMLTokens.TARGET);

                            // todo: valid to have a graph of just edges?
                            edgeOutVertex = Optional.ofNullable(graph.v(vertexIdOut))
                                    .orElseGet(() -> graph.addVertex(Element.ID, vertexIdOut));
                            edgeInVertex = Optional.ofNullable(graph.v(vertexIdIn))
                                    .orElseGet(() -> graph.addVertex(Element.ID, vertexIdIn));

                            isInEdge = true;
                            edgeProps = new HashMap<>();

                            break;
                        case GraphMLTokens.DATA:
                            final String key = reader.getAttributeValue(null, GraphMLTokens.KEY);
                            final String dataAttributeName = keyIdMap.get(key);

                            if (dataAttributeName != null) {
                                final String value = reader.getElementText();

                                if (isInVertex) {
                                    if (key.equals(vertexLabelKey))
                                        vertexLabel = value;
                                    else
                                        vertexProps.put(dataAttributeName, typeCastValue(key, value, keyTypesMaps));
                                } else if (isInEdge) {
                                    if (key.equals(edgeLabelKey))
                                        edgeLabel = value;
                                    else if (key.equals(edgeIdKey))
                                        edgeId = value;
                                    else
                                        edgeProps.put(dataAttributeName, typeCastValue(key, value, keyTypesMaps));
                                }
                            }

                            break;
                    }
                } else if (eventType.equals(XMLEvent.END_ELEMENT)) {
                    final String elementName = reader.getName().getLocalPart();

                    if (elementName.equals(GraphMLTokens.NODE)) {
                        final String currentVertexId = vertexId;
                        final String currentVertexLabel = Optional.ofNullable(vertexLabel).orElse(Element.DEFAULT_LABEL);
                        final Vertex currentVertex = graph.addVertex(Element.ID, currentVertexId, Element.LABEL, currentVertexLabel);

                        // todo: set properties all at once
                        for (Map.Entry<String, Object> prop : vertexProps.entrySet()) {
                            currentVertex.setProperty(prop.getKey(), prop.getValue());
                        }

                        vertexId = null;
                        vertexLabel = null;
                        vertexProps = null;
                        isInVertex = false;
                    } else if (elementName.equals(GraphMLTokens.EDGE)) {
                        final Edge currentEdge = edgeOutVertex.addEdge(edgeLabel, edgeInVertex, Element.ID, edgeId);

                        // todo: set properties all at once
                        for (Map.Entry<String, Object> prop : edgeProps.entrySet()) {
                            currentEdge.setProperty(prop.getKey(), prop.getValue());
                        }

                        edgeId = null;
                        edgeLabel = null;
                        edgeOutVertex = null;
                        edgeInVertex = null;
                        edgeProps = null;
                        isInEdge = false;
                    }

                }
            }

            graph.tx().commit();
        } catch (XMLStreamException xse) {
            throw new IOException(xse);
        }
    }

    private static Object typeCastValue(final String key, final String value, final Map<String, String> keyTypes) {
        final String type = keyTypes.get(key);
        if (null == type || type.equals(GraphMLTokens.STRING))
            return value;
        else if (type.equals(GraphMLTokens.FLOAT))
            return Float.valueOf(value);
        else if (type.equals(GraphMLTokens.INT))
            return Integer.valueOf(value);
        else if (type.equals(GraphMLTokens.DOUBLE))
            return Double.valueOf(value);
        else if (type.equals(GraphMLTokens.BOOLEAN))
            return Boolean.valueOf(value);
        else if (type.equals(GraphMLTokens.LONG))
            return Long.valueOf(value);
        else
            return value;
    }

    public static Builder create() {
        return new Builder();
    }

    /**
     * Allows configuration and construction of the GraphMLReader instance.
     */
    public static final class Builder {
        private String vertexIdKey = Element.ID;
        private String edgeIdKey = Element.ID;
        private boolean incrementalLoading = false;
        private String edgeLabelKey = GraphMLTokens.LABEL_E;
        private String vertexLabelKey = GraphMLTokens.LABEL_V;
        private long batchSize = BatchGraph.DEFAULT_BUFFER_SIZE;

        private Builder() {}

        public Builder vertexIdKey(final String vertexIdKey) {
            this.vertexIdKey = vertexIdKey;
            return this;
        }

        public Builder edgeIdKey(final String edgeIdKey) {
            this.edgeIdKey = edgeIdKey;
            return this;
        }

        public Builder edgeLabelKey(final String edgeLabelKey) {
            this.edgeLabelKey = edgeLabelKey;
            return this;
        }

        public Builder vertexLabelKey(final String vertexLabelKey) {
            this.vertexLabelKey = vertexLabelKey;
            return this;
        }

        public Builder batchSize(final long batchSize) {
            this.batchSize = batchSize;
            return this;
        }

        public Builder enableIncrementalLoading(final boolean enabled){
            this.incrementalLoading = enabled;
            return this;
        }

        public GraphMLReader build() {
            return new GraphMLReader(vertexIdKey, edgeIdKey, edgeLabelKey, vertexLabelKey, batchSize, incrementalLoading);
        }
    }
}
