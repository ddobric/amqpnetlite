﻿//  ------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation
//  All rights reserved. 
//  
//  Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this 
//  file except in compliance with the License. You may obtain a copy of the License at 
//  http://www.apache.org/licenses/LICENSE-2.0  
//  
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
//  EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR 
//  CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR 
//  NON-INFRINGEMENT. 
// 
//  See the Apache Version 2.0 License for specific language governing permissions and 
//  limitations under the License.
//  ------------------------------------------------------------------------------------

namespace Amqp.Serialization
{
    using System;
    using System.Collections;
#if !NETFX35
    using System.Collections.Concurrent;
#endif
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization;
    using Amqp.Types;

    /// <summary>
    /// Serializes and deserializes an instance of an AMQP type.
    /// The descriptor (name and code) is scoped to and must be
    /// uniqueue within an instance of the serializer.
    /// When the static Serialize and Deserialize methods are called,
    /// the default instance is used.
    /// </summary>
    public sealed class AmqpSerializer
    {
        static readonly AmqpSerializer instance = new AmqpSerializer();
        readonly ConcurrentDictionary<Type, SerializableType> typeCache;

        /// <summary>
        /// Initializes a new instance of the AmqpSerializer class.
        /// </summary>
        public AmqpSerializer()
        {
            this.typeCache = new ConcurrentDictionary<Type, SerializableType>();
        }

        /// <summary>
        /// Serializes an instance of an AMQP type into a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="graph">The serializable AMQP object.</param>
        public static void Serialize(ByteBuffer buffer, object graph)
        {
            WriteObject(instance, buffer, graph);
        }

        /// <summary>
        /// Deserializes an instance of an AMQP type from a buffer.
        /// </summary>
        /// <typeparam name="T">The serializable type.</typeparam>
        /// <param name="buffer">The buffer to read from.</param>
        /// <returns></returns>
        public static T Deserialize<T>(ByteBuffer buffer)
        {
            return ReadObject<T, T>(instance, buffer);
        }

        /// <summary>
        /// Deserializes an instance of an AMQP type from a buffer.
        /// </summary>
        /// <typeparam name="T">The serializable type.</typeparam>
        /// <typeparam name="TAs">The return type of the deserialized object.</typeparam>
        /// <param name="buffer">The buffer to read from.</param>
        /// <returns></returns>
        public static TAs Deserialize<T, TAs>(ByteBuffer buffer)
        {
            return ReadObject<T, TAs>(instance, buffer);
        }

        /// <summary>
        /// Writes an serializable object into a buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="graph">The serializable object.</param>
        public void WriteObject(ByteBuffer buffer, object graph)
        {
            WriteObject(this, buffer, graph);
        }

        /// <summary>
        /// Reads an serializable object from a buffer.
        /// </summary>
        /// <typeparam name="T">The type of the serializable object.</typeparam>
        /// <param name="buffer">The buffer to read.</param>
        /// <returns></returns>
        public T ReadObject<T>(ByteBuffer buffer)
        {
            return ReadObject<T, T>(this, buffer);
        }

        /// <summary>
        /// Reads an serializable object from a buffer.
        /// </summary>
        /// <typeparam name="T">The type of the serializable object.</typeparam>
        /// <typeparam name="TAs">The return type of the deserialized object.</typeparam>
        /// <param name="buffer">The buffer to read.</param>
        /// <returns></returns>
        public TAs ReadObject<T, TAs>(ByteBuffer buffer)
        {
            return ReadObject<T, TAs>(this, buffer);
        }

        internal SerializableType GetType(Type type)
        {
            return this.GetOrCompileType(type, false);
        }

        static void WriteObject(AmqpSerializer serializer, ByteBuffer buffer, object graph)
        {
            if (graph == null)
            {
                Encoder.WriteObject(buffer, null);
            }
            else
            {
                SerializableType type = serializer.GetType(graph.GetType());
                type.WriteObject(buffer, graph);
            }
        }

        static TAs ReadObject<T, TAs>(AmqpSerializer serializer, ByteBuffer buffer)
        {
            SerializableType type = serializer.GetType(typeof(T));
            return (TAs)type.ReadObject(buffer);
        }

        SerializableType GetOrCompileType(Type type, bool describedOnly)
        {
            SerializableType serialiableType = null;
            if (!this.typeCache.TryGetValue(type, out serialiableType))
            {
                serialiableType = this.CompileType(type, describedOnly);
                if (serialiableType != null)
                {
                    serialiableType = this.typeCache.GetOrAdd(type, serialiableType);
                }
            }

            if (serialiableType == null)
            {
                throw new NotSupportedException(type.FullName);
            }

            return serialiableType;
        }

        SerializableType CompileType(Type type, bool describedOnly)
        {
            AmqpContractAttribute contractAttribute = type.GetTypeInfo().GetCustomAttribute<AmqpContractAttribute>(false);
            if (contractAttribute == null)
            {
                if (describedOnly)
                {
                    return null;
                }
                else
                {
                    return CompileNonContractTypes(type);
                }
            }

            SerializableType baseType = null;
            if (type.GetTypeInfo().BaseType != typeof(object))
            {
                baseType = this.CompileType(type.GetTypeInfo().BaseType, true);
                if (baseType != null)
                {
                    if (baseType.Encoding != contractAttribute.Encoding)
                    {
                        throw new SerializationException(
                            Fx.Format("{0}.Encoding ({1}) is different from {2}.Encoding ({3})",
                                type.Name, contractAttribute.Encoding, type.GetTypeInfo().BaseType.Name, baseType.Encoding));
                    }

                    baseType = this.typeCache.GetOrAdd(type.GetTypeInfo().BaseType, baseType);
                }
            }

            string descriptorName = contractAttribute.Name;
            ulong? descriptorCode = contractAttribute.InternalCode;
            if (descriptorName == null && descriptorCode == null)
            {
                descriptorName = type.FullName;
            }

            List<SerialiableMember> memberList = new List<SerialiableMember>();
            if (baseType != null)
            {
                memberList.AddRange(baseType.Members);
            }

            int lastOrder = memberList.Count + 1;
            MemberInfo[] memberInfos = type.GetTypeInfo().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodAccessor[] serializationCallbacks = new MethodAccessor[SerializationCallback.Size];
            foreach (MemberInfo memberInfo in memberInfos)
            {
                if (memberInfo.DeclaringType != type)
                {
                    continue;
                }

                if (memberInfo is FieldInfo || memberInfo is PropertyInfo)
                {
                    AmqpMemberAttribute attribute = memberInfo.GetCustomAttribute<AmqpMemberAttribute>(true);
                    if (attribute == null)
                    {
                        continue;
                    }

                    SerialiableMember member = new SerialiableMember();
                    member.Name = attribute.Name ?? memberInfo.Name;
                    member.Order = attribute.InternalOrder ?? lastOrder++;
                    member.Accessor = MemberAccessor.Create(memberInfo, true);

                    // This will recursively resolve member types
                    Type memberType = memberInfo is FieldInfo ? ((FieldInfo)memberInfo).FieldType : ((PropertyInfo)memberInfo).PropertyType;
                    member.Type = GetType(memberType);

                    memberList.Add(member);
                }
                else if (memberInfo is MethodInfo)
                {
                    MethodInfo methodInfo = (MethodInfo)memberInfo;
                    MethodAccessor methodAccessor;
                    if (this.TryCreateMethodAccessor<OnSerializingAttribute>(methodInfo, out methodAccessor))
                    {
                        serializationCallbacks[SerializationCallback.OnSerializing] = methodAccessor;
                    }
                    else if (this.TryCreateMethodAccessor<OnSerializedAttribute>(methodInfo, out methodAccessor))
                    {
                        serializationCallbacks[SerializationCallback.OnSerialized] = methodAccessor;
                    }
                    else if (this.TryCreateMethodAccessor<OnDeserializingAttribute>(methodInfo, out methodAccessor))
                    {
                        serializationCallbacks[SerializationCallback.OnDeserializing] = methodAccessor;
                    }
                    else if (this.TryCreateMethodAccessor<OnDeserializedAttribute>(methodInfo, out methodAccessor))
                    {
                        serializationCallbacks[SerializationCallback.OnDeserialized] = methodAccessor;
                    }
                }
            }

            if (contractAttribute.Encoding == EncodingType.List)
            {
                memberList.Sort(MemberOrderComparer.Instance);
                int order = -1;
                foreach (SerialiableMember member in memberList)
                {
                    if (order > 0 && member.Order == order)
                    {
                        throw new SerializationException(Fx.Format("Duplicate Order {0} detected in {1}", order, type.Name));
                    }

                    order = member.Order;
                }
            }

            SerialiableMember[] members = memberList.ToArray();

            if (contractAttribute.Encoding == EncodingType.SimpleMap &&
                type.GetTypeInfo().GetCustomAttribute<AmqpProvidesAttribute>(false) != null)
            {
                throw new SerializationException(
                    Fx.Format("{0}: SimpleMap encoding does not include descriptors so it does not support AmqpProvidesAttribute.", type.Name));
            }

            if (contractAttribute.Encoding == EncodingType.SimpleList &&
                type.GetTypeInfo().GetCustomAttribute<AmqpProvidesAttribute>(false) != null)
            {
                throw new SerializationException(
                    Fx.Format("{0}: SimpleList encoding does not include descriptors so it does not support AmqpProvidesAttribute.", type.Name));
            }

            Dictionary<Type, SerializableType> knownTypes = null;
            var providesAttributes = type.GetTypeInfo().GetCustomAttributes<AmqpProvidesAttribute>(false);
            foreach (object o in providesAttributes)
            {
                AmqpProvidesAttribute knownAttribute = (AmqpProvidesAttribute)o;
                if (knownAttribute.Type.GetTypeInfo().GetCustomAttribute<AmqpContractAttribute>(false) != null)
                {
                    if (knownTypes == null)
                    {
                        knownTypes = new Dictionary<Type, SerializableType>();
                    }

                    // KnownType compilation is delayed and non-recursive to avoid circular references
                    knownTypes.Add(knownAttribute.Type, null);
                }
            }

            if (contractAttribute.Encoding == EncodingType.List)
            {
                return SerializableType.CreateDescribedListType(this, type, baseType, descriptorName,
                    descriptorCode, members, knownTypes, serializationCallbacks);
            }
            else if (contractAttribute.Encoding == EncodingType.Map)
            {
                return SerializableType.CreateDescribedMapType(this, type, baseType, descriptorName,
                    descriptorCode, members, knownTypes, serializationCallbacks);
            }
            else if (contractAttribute.Encoding == EncodingType.SimpleMap)
            {
                return SerializableType.CreateDescribedSimpleMapType(this, type, baseType, members, serializationCallbacks);
            }
            else if (contractAttribute.Encoding == EncodingType.SimpleList)
            {
                return SerializableType.CreateDescribedSimpleListType(this, type, baseType, members, serializationCallbacks);
            }
            else
            {
                throw new NotSupportedException(contractAttribute.Encoding.ToString());
            }
        }

        bool TryCreateMethodAccessor<T>(MethodInfo methodInfo, out MethodAccessor methodAccessor) where T : Attribute
        {
            T memberAttribute = methodInfo.GetCustomAttribute<T>(false);
            if (memberAttribute != null)
            {
                methodAccessor = MethodAccessor.Create((MethodInfo)methodInfo);
                return true;
            }

            methodAccessor = null;
            return false;
        }

        SerializableType CompileNonContractTypes(Type type)
        {
            // built-in type
            Encode encoder;
            Decode decoder;
            if (Encoder.TryGetCodec(type, out encoder, out decoder))
            {
                return SerializableType.CreatePrimitiveType(type, encoder, decoder);
            }

            if (type == typeof(object))
            {
                return SerializableType.CreateObjectType(type);
            }

            if (typeof(Described).GetTypeInfo().IsAssignableFrom(type))
            {
                return SerializableType.CreateObjectType(type);
            }

            if (typeof(IAmqpSerializable).GetTypeInfo().IsAssignableFrom(type))
            {
                return SerializableType.CreateAmqpSerializableType(this, type);
            }

            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type[] argTypes = type.GetTypeInfo().GetGenericArguments();
                Fx.Assert(argTypes.Length == 1, "Nullable type must have one argument");
                Type argType = argTypes[0];
                if (argType.GetTypeInfo().IsEnum)
                {
                    return CompileEnumType(argType);
                }
                else
                {
                    return SerializableType.CreateObjectType(type);
                }
            }

            if (type.GetTypeInfo().IsEnum)
            {
                return CompileEnumType(type);
            }

            SerializableType collection = this.CompileCollectionTypes(type);
            if (collection != null)
            {
                return collection;
            }

            return null;
        }

        SerializableType CompileEnumType(Type type)
        {
            SerializableType underlyingType = GetType(Enum.GetUnderlyingType(type));
            return SerializableType.CreateEnumType(type, underlyingType);
        }

        SerializableType CompileCollectionTypes(Type type)
        {
            MemberAccessor keyAccessor = null;
            MemberAccessor valueAccessor = null;
            MethodAccessor addAccess = null;
            Type itemType = null;

            foreach (Type it in type.GetTypeInfo().GetInterfaces())
            {
                if (it.GetTypeInfo().IsGenericType)
                {
                    Type genericTypeDef = it.GetGenericTypeDefinition();
                    if (genericTypeDef == typeof(IDictionary<,>))
                    {
                        Type[] argTypes = it.GetTypeInfo().GetGenericArguments();
                        itemType = typeof(KeyValuePair<,>).MakeGenericType(argTypes);
                        keyAccessor = MemberAccessor.Create(itemType.GetTypeInfo().GetProperty("Key"), false);
                        valueAccessor = MemberAccessor.Create(itemType.GetTypeInfo().GetProperty("Value"), false);
                        addAccess = MethodAccessor.Create(type.GetTypeInfo().GetMethod("Add", argTypes));

                        return SerializableType.CreateGenericMapType(this, type, keyAccessor, valueAccessor, addAccess);
                    }
                    else if (genericTypeDef == typeof(IList<>))
                    {
                        Type[] argTypes = it.GetTypeInfo().GetGenericArguments();
                        itemType = argTypes[0];
                        addAccess = MethodAccessor.Create(type.GetTypeInfo().GetMethod("Add", argTypes));

                        return SerializableType.CreateGenericListType(this, type, itemType, addAccess);
                    }
                }
            }

            return null;
        }

        sealed class MemberOrderComparer : IComparer<SerialiableMember>
        {
            public static readonly MemberOrderComparer Instance = new MemberOrderComparer();

            public int Compare(SerialiableMember m1, SerialiableMember m2)
            {
                return m1.Order == m2.Order ? 0 : (m1.Order > m2.Order ? 1 : -1);
            }
        }

#if NETFX35
        // this is for use within the serializer class only
        // ensure only the synchronized methods are called
        class ConcurrentDictionary<TKey, TValue> : Dictionary<TKey, TValue>
        {
            readonly object syncRoot;

            public ConcurrentDictionary()
            {
                this.syncRoot = new object();
            }

            public new bool TryGetValue(TKey key, out TValue value)
            {
                lock (this.syncRoot)
                {
                    return base.TryGetValue(key, out value);
                }
            }

            public TValue GetOrAdd(TKey key, TValue value)
            {
                lock (this.syncRoot)
                {
                    TValue temp;
                    if (base.TryGetValue(key, out temp))
                    {
                        return temp;
                    }
                    else
                    {
                        base.Add(key, value);
                        return value;
                    }
                }
            }
        }
#endif
    }
}
