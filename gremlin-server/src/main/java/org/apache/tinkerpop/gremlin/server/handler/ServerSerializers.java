/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
package org.apache.tinkerpop.gremlin.server.handler;

import org.apache.tinkerpop.gremlin.driver.MessageSerializer;
import org.apache.tinkerpop.gremlin.driver.ser.GraphSONMessageSerializerV1d0;
import org.apache.tinkerpop.gremlin.driver.ser.Serializers;

/**
 * @author Stephen Mallette (http://stephen.genoprime.com)
 */
final class ServerSerializers {

    private ServerSerializers() {}

    /**
     * Default serializer used by the server when the serializer requested does not match what is on the server.
     * Using GraphSON 1.0 on 3.3.5 because that's what it has long been set to in previous versions on
     * {@link Serializers#DEFAULT_RESULT_SERIALIZER} which is now deprecated.
     */
    static final MessageSerializer DEFAULT_SERIALIZER = new GraphSONMessageSerializerV1d0();

}
