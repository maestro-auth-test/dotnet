﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/***************************************************************************\
*
* Purpose:  Used to Keep track of Id to Object mappings in a BAML file.
*
\***************************************************************************/

using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;

#if PBTCOMPILER
using MS.Internal.Markup;
#else
using System.Windows;
using System.Windows.Threading;

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Security;
using MS.Internal.PresentationFramework;

#endif

#if PBTCOMPILER
namespace MS.Internal.Markup
#else
namespace System.Windows.Markup
#endif
{
    // Used to read/write mapping Baml Ids
    // to Assembly, types, namespaces, etc.
    internal class BamlMapTable
    {
        // Welcome to the world of "known types".
        // Known Types are used to bypass the cost of reflection for the
        // types introduced by the system.

        #region Constructor

        internal BamlMapTable(XamlTypeMapper xamlTypeMapper)
        {
            Debug.Assert(null != xamlTypeMapper);

            _xamlTypeMapper = xamlTypeMapper;

            // Setup the assembly record for the known types of controls
            _knownAssemblyInfoRecord = new BamlAssemblyInfoRecord
            {
                AssemblyId = -1,
                Assembly = ReflectionHelper.LoadAssembly(_frameworkAssembly, string.Empty)
            };
            _knownAssemblyInfoRecord.AssemblyFullName = _knownAssemblyInfoRecord.Assembly.FullName;
        }

        #endregion Constructor

        #region KnownTypes

#if !PBTCOMPILER
        // Creates an instance of a known type given the inverse of the known type ID. (which
        // is the negative short value of the KnownElements enum)
        internal object CreateKnownTypeFromId(short id)
        {
            if (id < 0)
            {
                return KnownTypes.CreateKnownElement((KnownElements)(-id));
            }

            return null;
        }
#endif
        // Returns known Type given the inverse of the known type ID (which
        // is the negative short value of the KnownElements enum)
        internal static Type GetKnownTypeFromId(short id)
        {
            if (id < 0)
            {
                return KnownTypes.Types[-id];
            }

            return null;
        }

        // Binary search through the KnowElements.Types[] table.
        // KE.Types[] is lazily loaded so the act of searching will load some types.
        // but this is considered better (memory wise) than having a seperate table.
        internal static short GetKnownTypeIdFromName(
            string assemblyFullName,
            string clrNamespace,
            string typeShortName)
        {
            if (typeShortName == string.Empty)
            {
                return 0;
            }

            int high = (int)KnownElements.MaxElement - 1;
            int low = 1;

            while (low <= high)
            {
                int probe = (high + low) / 2;
                Type probeType = KnownTypes.Types[probe];
                int cmp = string.CompareOrdinal(typeShortName, probeType.Name);
                if (cmp == 0)
                {
                    // Found a potential match.  Now compare the namespaces & assembly to be sure
#if !PBTCOMPILER
                    if (probeType.Namespace == clrNamespace
                        &&
                        probeType.Assembly.FullName == assemblyFullName )
#else
                    if (probeType.Namespace == clrNamespace)
#endif
                    {
                        return (short)-probe;
                    }
                    else
                    {
                        return 0;
                    }
                }

                if (cmp < 0)  // typeName < probeType.Name
                {
                    high = probe - 1;
                }
                else
                {
                    Debug.Assert(cmp > 0);
                    low = probe + 1;
                }
            }
            return 0;
        }

        // Return the ID from the type-to-KnownElements for the passed Type
        internal static short GetKnownTypeIdFromType(Type type)
        {
            if (type == null)
            {
                return 0;
            }

            return GetKnownTypeIdFromName(type.Assembly.FullName, type.Namespace, type.Name);
        }

        // Search through the known strings for a match to the passed string.
        // Return the string's index in the table if found.
        private static Int16 GetKnownStringIdFromName(string stringValue)
        {
            int end = _knownStrings.Length;
            // Indicies are one based, even though the _knownStrings array is
            // zero based.  So the first element in the _knownStrings array is null
            for (int i = 1; i < end; i++)
            {
                if (_knownStrings[i] == stringValue)
                {
                    return (short)-i;
                }
            }

            // return 0 if it is not a known string.
            return 0;
        }
        #endregion KnownTypes

        #region KnownConverters

        // Known Converters are used to avoid the cost of reflecting for
        // custom attributes on commonly used TypeConverters at load time.

#if !PBTCOMPILER
        // Returns the ID of the known TypeConverter for a given object type.  If there is no known
        // converter, return KnownElements.UnknownElement.
        internal static KnownElements GetKnownTypeConverterIdFromType(Type type)
        {
            KnownElements tcId;
            if (ReflectionHelper.IsNullableType(type))
            {
                tcId = KnownElements.NullableConverter;
            }
            else if (type == typeof(System.Type))
            {
                tcId = KnownElements.TypeTypeConverter;
            }
            else
            {
                short idNumber = GetKnownTypeIdFromType(type);

                if (idNumber < 0)
                {
                    tcId = KnownTypes.GetKnownTypeConverterId((KnownElements)(-idNumber));
                }
                else
                {
                    tcId = KnownElements.UnknownElement;
                }
            }

            return tcId;
        }

        // Returns the known TypeConverter for a given object type.  If there is no known
        // converter, return null.
        internal TypeConverter GetKnownConverterFromType(Type type)
        {
            KnownElements tcId = GetKnownTypeConverterIdFromType(type);

            TypeConverter converter;
            if (tcId != KnownElements.UnknownElement)
            {
                converter = GetConverterFromId((short)-(short)tcId, type, null);
            }
            else
            {
                converter = null;
            }

            return converter;
        }

        // Returns the known TypeConverter for a given object type.  If there is no known
        // converter, return null.  This is a static version of the previous
        // method for use when there is no parser context available.
        internal static TypeConverter GetKnownConverterFromType_NoCache(Type type)
        {
            KnownElements typeId = GetKnownTypeConverterIdFromType(type);

            TypeConverter tc;
            // The EnumConverter and NullableConverter need to be created specially, since
            // they need the type of the Enum or Nullable in their constructor.
            switch (typeId)
            {
                case KnownElements.EnumConverter:
                    Debug.Assert(type.IsEnum);
                    tc = new System.ComponentModel.EnumConverter(type);
                    break;

                case KnownElements.NullableConverter:
                    Debug.Assert(ReflectionHelper.IsNullableType(type));
                    tc = new System.ComponentModel.NullableConverter(type);
                    break;

                case KnownElements.UnknownElement:
                    tc = null;
                    break;

                default:
                    tc = KnownTypes.CreateKnownElement(typeId) as TypeConverter;
                    break;
            }

            return tc;
        }
#endif

        // Returns the known converter type for a given object type. If there is no known
        // converter, return null.
        internal Type GetKnownConverterTypeFromType(Type type)
        {
#if PBTCOMPILER
            // Need to handle Nullable types specially as they require a ctor that
            // takes the underlying type, but at compile time we only need to know the Type
            // of the Converter and not an actual instance of it.
            // Need to handle URI types specially since we can't reflect for their TypeConverter
            // in the ReflectionOnly Load (ROL) context.
            if (ReflectionHelper.IsNullableType(type))
            {
                return typeof(NullableConverter);
            }
            // The param 'type' will be in the ROL context while any typeof types will resolve against
            // the real type that is loaded with PBT. So we need to first get the ROL type from the real
            // type before doing the check.
            else if (type == ReflectionHelper.GetSystemType(typeof(Uri)))
            {
                return typeof(UriTypeConverter);
            }
            else
#endif
            if (type == typeof(System.Type))
            {
                return typeof(TypeTypeConverter);
            }

            short idNumber = GetKnownTypeIdFromType(type);
            if (idNumber == 0)
            {
                return null;
            }

            KnownElements id = (KnownElements)(-idNumber);

            KnownElements tcId = KnownTypes.GetKnownTypeConverterId(id);
            if (tcId == KnownElements.UnknownElement)
            {
                return null;
            }

            return KnownTypes.Types[(int)tcId];
        }

        // Return the known converter type for the given property and property owner.
        // Return null if there is no property level converter.  In that case, the type
        // level converter is normally used by calling GetKnownConverterTypeFromType()
        private static Type GetKnownConverterTypeFromPropName(
            Type propOwnerType,
            string propName)
        {
            short idNumber = GetKnownTypeIdFromType(propOwnerType);
            if (idNumber == 0)
            {
                return null;
            }

            KnownElements id = (KnownElements)(-idNumber);

            KnownElements converterId = KnownTypes.GetKnownTypeConverterIdForProperty(id, propName);
            if (converterId == KnownElements.UnknownElement)
            {
                return null;
            }

            return KnownTypes.Types[(int)converterId];
        }

        #endregion KnownConverters

        #region Methods

#if !PBTCOMPILER
        // This is called when a parse is begun when the very first baml record is
        // processed.  If the BamlMapTable already contains data, then this means
        // it is being re-used for multiple parses.  In this case set the _reusingMapTable
        // flag and make certain that the ObjectHashTable is populated before clearing
        // the existing assembly, type and property lists for the next parse.
        internal void Initialize()
        {
            if (AttributeIdMap.Count > 0 || TypeIdMap.Count > 0)
            {
                _reusingMapTable = true;
                // Populate the ObjectHashTable here only after the first parse has
                // completed and the second is about to begin.  This is done so that
                // a single parse does not pay the price of having the hash table, and
                // the second through 'n' parses are added as they are read in.
                if (ObjectHashTable.Count == 0)
                {
                    // Loop through attributes. We only care about having CLR properties in
                    // the hash table.  DependencyProperties are already cached by the framework.
                    for (int i = 0; i < AttributeIdMap.Count; i++)
                    {
                        BamlAttributeInfoRecord info = AttributeIdMap[i];
                        if (info.PropInfo != null)
                        {
                            object key = GetAttributeInfoKey(info.OwnerType.FullName, info.Name);
                            ObjectHashTable.Add(key, info);
                        }
                    }

                    // Loop through types and cache them.
                    for (int j = 0; j < TypeIdMap.Count; j++)
                    {
                        BamlTypeInfoRecord info = TypeIdMap[j];
                        if (info.Type != null)
                        {
                            BamlAssemblyInfoRecord assyInfo = GetAssemblyInfoFromId(info.AssemblyId);
                            TypeInfoKey key = GetTypeInfoKey(assyInfo.AssemblyFullName, info.TypeFullName);
                            ObjectHashTable.Add(key, info);
                        }
                    }
                }
            }
            AssemblyIdMap.Clear();
            TypeIdMap.Clear();
            AttributeIdMap.Clear();
            StringIdMap.Clear();
        }

        // Given an Id looks up the Type in the MapTable.  This works for known types
        // and types that are a part of BamlTypeInfoRecords in the baml file.
        internal Type GetTypeFromId(short id)
        {
            if (id < 0)
            {
                return KnownTypes.Types[-id];
            }

            BamlTypeInfoRecord typeInfo = TypeIdMap[id];
            Type type = GetTypeFromTypeInfo(typeInfo);

            if (type is null)
            {
                ThrowException(nameof(SR.ParserFailFindType), typeInfo.TypeFullName);
            }

            return type;
        }

        // Certain known types have serializers.  Return true if the passed type
        // is one of those.
        // NOTE: When serializers are publically extensible, then this should be
        //       reflected for as part of the KnownElementsInitializer.
        internal bool HasSerializerForTypeId(short id)
        {
            if (id >= 0)
            {
                return false;
            }

            if (-id == (short)KnownElements.Style ||

                -id == (short)KnownElements.FrameworkTemplate ||
                -id == (short)KnownElements.ControlTemplate ||
                -id == (short)KnownElements.DataTemplate ||
                -id == (short)KnownElements.HierarchicalDataTemplate ||
                -id == (short)KnownElements.ItemsPanelTemplate)
            {
                return true;
            }

            return false;
       }

        // Return a type info record for a type identified by the passed ID.  If the
        // ID is negative, then this is a known type, so manufacture a BamlTypeInfoRecord
        // for it.
        internal BamlTypeInfoRecord GetTypeInfoFromId(short id)
        {
            // If the id is less than 0, it is a known type that lives in the
            // known assembly, so manufacture a type info record for it.
            if (id < 0)
            {
                // For some types, we create a TypeInfo record with serializer information.  For
                // all other types, we create a regular TypeInfo record.
                // NOTE: When serializers are publically extensible, then this should be
                //       reflected for as part of the KnownElementsInitializer.
                BamlTypeInfoRecord info;
                if (-id == (short)KnownElements.Style)
                {
                    info = new BamlTypeInfoWithSerializerRecord();
                    ((BamlTypeInfoWithSerializerRecord)info).SerializerTypeId =
                                          -(int) KnownElements.XamlStyleSerializer;
                    ((BamlTypeInfoWithSerializerRecord)info).SerializerType =
                                          KnownTypes.Types[(int)KnownElements.XamlStyleSerializer];
                    info.AssemblyId = -1;
                }
                else if (-id == (short)KnownElements.ControlTemplate ||
                         -id == (short)KnownElements.DataTemplate ||
                         -id == (short)KnownElements.HierarchicalDataTemplate ||
                         -id == (short)KnownElements.ItemsPanelTemplate)
                {
                    info = new BamlTypeInfoWithSerializerRecord();
                    ((BamlTypeInfoWithSerializerRecord)info).SerializerTypeId =
                                          -(int) KnownElements.XamlTemplateSerializer;
                    ((BamlTypeInfoWithSerializerRecord)info).SerializerType =
                                          KnownTypes.Types[(int)KnownElements.XamlTemplateSerializer];
                    info.AssemblyId = -1;
                }
                else
                {
                    // Search the Assembly table to see if this type has a match.  If not, then
                    // we know that it is a known assembly for an Avalon known type.  We have to do
                    // this since some of the known types are not avalon types, such as Bool, Object,
                    // Double, and others...
                    info = new BamlTypeInfoRecord
                    {
                        AssemblyId = GetAssemblyIdForType(KnownTypes.Types[-id])
                    };
                }

                info.TypeId = id;
                info.Type = KnownTypes.Types[-id];
                info.TypeFullName = info.Type.FullName;
                return info;
            }
            else
            {
                return TypeIdMap[id];
            }
        }

        // Search the Assembly table to see if this type has a match.  If not, then
        // we know that it is a known assembly for an Avalon known type.  We have to do
        // this since some of the known types are not avalon types, such as Bool, Object,
        // Double, and others...
        private short GetAssemblyIdForType(Type t)
        {
            string assemblyName = t.Assembly.FullName;
            for (int i = 0; i < AssemblyIdMap.Count; i++)
            {
                string mapName = AssemblyIdMap[i].AssemblyFullName;
                if (mapName == assemblyName)
                {
                    return (short)i;
                }
            }

            return -1;  // Default known assembly, if assembly is not one of those
                        // already known.
        }

        // Return an instance of a TypeConverter object, given the type id of the object.
        // This may be a known element, or one that is stored in a type record
        internal TypeConverter GetConverterFromId (
            short typeId,
            Type  propType,
            ParserContext pc)
        {
            TypeConverter tc = null;
            if (typeId < 0)
            {
                // The EnumConverter and NullableConverter need to be created specially, since
                // they need the type of the Enum or Nullable in their constructor.
                switch((KnownElements)(-typeId))
                {
                    case KnownElements.EnumConverter:

                        Debug.Assert(propType.IsEnum);
                        tc = GetConverterFromCache(propType);
                        if (tc == null)
                        {
                            tc = new System.ComponentModel.EnumConverter(propType);
                            ConverterCache.Add(propType, tc);
                        }
                        break;

                    case KnownElements.NullableConverter:

                        Debug.Assert(ReflectionHelper.IsNullableType(propType));
                        tc = GetConverterFromCache(propType);
                        if (tc == null)
                        {
                            tc = new System.ComponentModel.NullableConverter(propType);
                            ConverterCache.Add(propType, tc);
                        }

                        break;

                    default:

                        tc = GetConverterFromCache(typeId);
                        if (tc == null)
                        {
                            tc = CreateKnownTypeFromId(typeId) as TypeConverter;
                            ConverterCache.Add(typeId, tc);
                        }

                        break;
                }
            }
            else
            {
                Type t = GetTypeFromId(typeId);
                tc = GetConverterFromCache(t);
                if (tc == null)
                {
                    if (ReflectionHelper.IsPublicType(t))
                    {
                        tc = Activator.CreateInstance(t) as TypeConverter;
                    }
                    else
                    {
                        tc = XamlTypeMapper.CreateInternalInstance(pc, t) as TypeConverter;
                    }

                    if (tc == null)
                    {
                        ThrowException(nameof(SR.ParserNoTypeConv), propType.Name);
                    }
                    else
                    {
                        ConverterCache.Add(t, tc);
                    }
                }
            }

            return tc;
        }

        // Get the string Info from the String Table.  This may be one of the few
        // known strings that are not written out to BAML, or may be a string in the
        // string table.
        internal string GetStringFromStringId(int id)
        {
            if (id < 0)
            {
                Debug.Assert(-id <= _knownStrings.Length);
                return _knownStrings[-id];
            }
            else
            {
                Debug.Assert(id < StringIdMap.Count);
                BamlStringInfoRecord infoRecord = StringIdMap[id];
                return infoRecord.Value;
            }
        }

        // Return attribute info record given an ID.
        internal BamlAttributeInfoRecord GetAttributeInfoFromId(short id)
        {
            if (id < 0)
            {
                KnownProperties knownId = (KnownProperties)(-id);
                BamlAttributeInfoRecord record = new BamlAttributeInfoRecord
                {
                    AttributeId = id,
                    OwnerTypeId = (short)-(short)KnownTypes.GetKnownElementFromKnownCommonProperty(knownId)
                };
                GetAttributeOwnerType(record); // This will update the OwnerType property
                record.Name = GetAttributeNameFromKnownId(knownId);

                if(knownId < KnownProperties.MaxDependencyProperty)
                {
                    DependencyProperty dp = KnownTypes.GetKnownDependencyPropertyFromId(knownId);
                    record.DP = dp;
                }
                else
                {
                    Type ownerType = record.OwnerType;
                    record.PropInfo = ownerType.GetProperty(record.Name, BindingFlags.Instance | BindingFlags.Public);
                }
                return record;
            }

            return AttributeIdMap[id];
        }

        internal BamlAttributeInfoRecord GetAttributeInfoFromIdWithOwnerType(short attributeId)
        {
            BamlAttributeInfoRecord record = GetAttributeInfoFromId(attributeId);
            GetAttributeOwnerType(record); // This will update the OwnerType property
            return record;
        }

        private string GetAttributeNameFromKnownId(KnownProperties knownId)
        {
            if(knownId < KnownProperties.MaxDependencyProperty)
            {
                DependencyProperty dp = KnownTypes.GetKnownDependencyPropertyFromId(knownId);
                return dp.Name;
            }
            else
            {
                return KnownTypes.GetKnownClrPropertyNameFromId(knownId);
            }
        }

        internal string GetAttributeNameFromId(short id)
        {
            if (id < 0)
            {
                return GetAttributeNameFromKnownId((KnownProperties)(-id));
            }
            else
            {
                BamlAttributeInfoRecord record = AttributeIdMap[id];
                return record.Name;
            }
        }

        internal bool DoesAttributeMatch(short id, short ownerTypeId, string name)
        {
            if (id < 0)
            {
                KnownProperties knownId = (KnownProperties)(-id);
                string propertyName = GetAttributeNameFromKnownId(knownId);
                KnownElements knownElement = KnownTypes.GetKnownElementFromKnownCommonProperty(knownId);
                return  (ownerTypeId == -(short)knownElement && (string.Equals(propertyName, name, StringComparison.Ordinal)));
            }
            else
            {
                BamlAttributeInfoRecord record = AttributeIdMap[id];
                return (record.OwnerTypeId == ownerTypeId) && (string.Equals(record.Name, name, StringComparison.Ordinal));
            }
        }

        internal bool DoesAttributeMatch(short id, string name)
        {
            string propertyName = GetAttributeNameFromId(id);
            if (null == propertyName)
                return false;
            return (string.Equals(propertyName, name, StringComparison.Ordinal));
        }

        internal bool DoesAttributeMatch(short id, BamlAttributeUsage attributeUsage)
        {
            if (id < 0)
            {
                return attributeUsage == GetAttributeUsageFromKnownAttribute((KnownProperties)(-id));
            }
            else
            {
                BamlAttributeInfoRecord record = AttributeIdMap[id];
                return attributeUsage == record.AttributeUsage;
            }
        }

        internal void GetAttributeInfoFromId(short id, out short ownerTypeId, out string name, out BamlAttributeUsage attributeUsage)
        {
            if (id < 0)
            {
                KnownProperties knownId = (KnownProperties)(-id);
                name = GetAttributeNameFromKnownId(knownId);
                ownerTypeId = (short)-(short)KnownTypes.GetKnownElementFromKnownCommonProperty(knownId);
                attributeUsage = GetAttributeUsageFromKnownAttribute(knownId);
            }
            else
            {
                BamlAttributeInfoRecord record = AttributeIdMap[id];
                name = record.Name;
                ownerTypeId = record.OwnerTypeId;
                attributeUsage = record.AttributeUsage;
            }
        }

        private static BamlAttributeUsage GetAttributeUsageFromKnownAttribute(KnownProperties knownId)
        {
            if (knownId == KnownProperties.FrameworkElement_Name)
            {
                return BamlAttributeUsage.RuntimeName;
            }
            else
            {
                return BamlAttributeUsage.Default;
            }
        }

        // Return the Type given a type info record.  The Type is cached in the info record.
        internal Type GetTypeFromTypeInfo(BamlTypeInfoRecord typeInfo)
        {
            if (null == typeInfo.Type)
            {
                BamlAssemblyInfoRecord assemblyInfoRecord = GetAssemblyInfoFromId(typeInfo.AssemblyId);

                if (null != assemblyInfoRecord)
                {
                    // Check for cached type in object hash table.
                    TypeInfoKey key = GetTypeInfoKey(assemblyInfoRecord.AssemblyFullName, typeInfo.TypeFullName);
                    BamlTypeInfoRecord cachedTypeInfo = GetHashTableData(key) as BamlTypeInfoRecord;
                    if (cachedTypeInfo != null && cachedTypeInfo.Type != null)
                    {
                        typeInfo.Type = cachedTypeInfo.Type;
                    }
                    else
                    {
                        Assembly assembly = GetAssemblyFromAssemblyInfo(assemblyInfoRecord);
                        if (null != assembly)
                        {
                            Type type = assembly.GetType(typeInfo.TypeFullName);
                            typeInfo.Type = type;
                            AddHashTableData(key, typeInfo);
                        }
                    }
                }
            }

            return typeInfo.Type;
        }

        // Return the attribute owner Type given an attribute info record.
        // The owner Type is cached in the info record.
        private Type GetAttributeOwnerType(BamlAttributeInfoRecord bamlAttributeInfoRecord)
        {
            if (bamlAttributeInfoRecord.OwnerType == null)
            {
                if (bamlAttributeInfoRecord.OwnerTypeId < 0)
                {
                    bamlAttributeInfoRecord.OwnerType = GetKnownTypeFromId(bamlAttributeInfoRecord.OwnerTypeId);
                }
                else
                {
                    BamlTypeInfoRecord typeInfo = TypeIdMap[bamlAttributeInfoRecord.OwnerTypeId];
                    bamlAttributeInfoRecord.OwnerType = GetTypeFromTypeInfo(typeInfo);
                }
            }

            return bamlAttributeInfoRecord.OwnerType;
        }

        internal Type GetCLRPropertyTypeAndNameFromId(short attributeId, out string propName)
        {
            propName = null;
            Type propType = null;
            Debug.Assert(attributeId >= 0, "Known Property Id must be a DependencyProperty.");

            BamlAttributeInfoRecord attributeInfo = GetAttributeInfoFromIdWithOwnerType(attributeId);
            if (attributeInfo != null && attributeInfo.OwnerType != null)
            {
                // Update the CLR propInfo into the AttributeInfoRecord.
                XamlTypeMapper.UpdateClrPropertyInfo(attributeInfo.OwnerType, attributeInfo);
                // Get the property Type from the Info.
                propType = attributeInfo.GetPropertyType();
            }
            else
            {
                propName = string.Empty;
            }

            if (propType == null)
            {
                if (propName == null)
                {
                    propName = $"{attributeInfo.OwnerType.FullName}.{attributeInfo.Name}";
                }

                ThrowException(nameof(SR.ParserNoPropType), propName);
            }
            else
            {
                propName = attributeInfo.Name;
            }

            return propType;
        }

        internal DependencyProperty GetDependencyPropertyValueFromId(short memberId, string memberName, out Type declaringType)
        {
            declaringType = null;
            DependencyProperty dp = null;

            if (memberName == null)
            {
                Debug.Assert(memberId < 0);
                KnownProperties knownId = (KnownProperties)(-memberId);
                if (knownId < KnownProperties.MaxDependencyProperty
                    || knownId == KnownProperties.Run_Text)
                {
                    dp = KnownTypes.GetKnownDependencyPropertyFromId(knownId);
                }
            }
            else
            {
                declaringType = GetTypeFromId(memberId);
                dp = DependencyProperty.FromName(memberName, declaringType);
            }

            return dp;
        }

        // Finds a DependencyProperty from the Known tables or attribInfo
        internal DependencyProperty GetDependencyPropertyValueFromId(short memberId)
        {
            DependencyProperty dp = null;
            if (memberId < 0)
            {
                KnownProperties knownId = (KnownProperties)(-memberId);
                if (knownId < KnownProperties.MaxDependencyProperty)
                {
                    dp = KnownTypes.GetKnownDependencyPropertyFromId(knownId);
                }
            }

            if (dp == null)
            {
                string name;
                short ownerTypeId;
                BamlAttributeUsage attributeUsage;

                GetAttributeInfoFromId(memberId,
                                       out ownerTypeId,
                                       out name,
                                       out attributeUsage);
                Type declaringType = GetTypeFromId(ownerTypeId);
                dp = DependencyProperty.FromName(name, declaringType);
            }

            return dp;
        }

        internal DependencyProperty GetDependencyProperty(int id)
        {
            if (id < 0)
            {
                return KnownTypes.GetKnownDependencyPropertyFromId((KnownProperties)(-id));
            }
            else
            {
                BamlAttributeInfoRecord attribInfo = AttributeIdMap[id];
                return GetDependencyProperty(attribInfo);
            }
        }

        // Return the DependencyProperty given an attribute info record.  The DP
        // is cached in the info record.
        internal DependencyProperty GetDependencyProperty(BamlAttributeInfoRecord bamlAttributeInfoRecord)
        {
            if ((null == bamlAttributeInfoRecord.DP) && (null == bamlAttributeInfoRecord.PropInfo))
            {
                // This will update the record
                GetAttributeOwnerType(bamlAttributeInfoRecord);

                if (null != bamlAttributeInfoRecord.OwnerType)
                {
                    bamlAttributeInfoRecord.DP = DependencyProperty.FromName(
                                                                bamlAttributeInfoRecord.Name,
                                                                bamlAttributeInfoRecord.OwnerType);
                }
            }

            return bamlAttributeInfoRecord.DP;
        }

        // Return the RoutedEvent given an attribute info record.  The RoutedEvent
        // is cached in the info record.
        internal RoutedEvent GetRoutedEvent(BamlAttributeInfoRecord bamlAttributeInfoRecord)
        {
            if (null == bamlAttributeInfoRecord.Event)
            {
                Type ownerType = GetAttributeOwnerType(bamlAttributeInfoRecord);

                if (null != ownerType)
                {
                    bamlAttributeInfoRecord.Event = XamlTypeMapper.RoutedEventFromName(bamlAttributeInfoRecord.Name,ownerType);
                }
            }

            return bamlAttributeInfoRecord.Event;
        }

#endif

        internal short GetAttributeOrTypeId(BinaryWriter binaryWriter, Type declaringType, string memberName, out short typeId)
        {
            short propertyId = 0;
            if (!GetTypeInfoId(binaryWriter,
                               declaringType.Assembly.FullName,
                               declaringType.FullName,
                               out typeId))
            {
                typeId = AddTypeInfoMap(binaryWriter,
                                        declaringType.Assembly.FullName,
                                        declaringType.FullName,
                                        declaringType,
                                        string.Empty,
                                        string.Empty);
            }
            else if (typeId < 0)
            {
                // Only known types will have known properties
                propertyId = (short)-KnownTypes.GetKnownPropertyAttributeId((KnownElements)(-typeId), memberName);
            }

            return propertyId;
        }

        // Return an assembly info record for the assembly identified by the passed ID
        internal BamlAssemblyInfoRecord GetAssemblyInfoFromId(short id)
        {
            // If the id is -1, then it is in the known assembly where all of
            // the Avalon controls are defined.  In that
            // case return the known assembly info record.
            if (id == -1)
            {
                return _knownAssemblyInfoRecord;
            }
            else
            {
                return AssemblyIdMap[id];
            }
        }

        // Return the Assembly from the passed Assembly info record.  This is cached in
        // the info record.
        private Assembly GetAssemblyFromAssemblyInfo(BamlAssemblyInfoRecord assemblyInfoRecord)
        {
            if (null == assemblyInfoRecord.Assembly)
            {
                string path = XamlTypeMapper.AssemblyPathFor(assemblyInfoRecord.AssemblyFullName);
                assemblyInfoRecord.Assembly = ReflectionHelper.LoadAssembly(assemblyInfoRecord.AssemblyFullName, path);
            }

            return assemblyInfoRecord.Assembly;
        }

        // assembly maps
        // mapping of ids to Assembly Names and Assembly reference
        // create an entry for every referenced assembly passed in on compile
        // in case no tags reference it at compile but do at Load we won't need
        // to be given this information again.

        internal BamlAssemblyInfoRecord AddAssemblyMap(
            BinaryWriter binaryWriter,
            string assemblyFullName)
        {
            Debug.Assert(assemblyFullName.Length != 0, "empty assembly");

            AssemblyInfoKey key = new AssemblyInfoKey
            {
                AssemblyFullName = assemblyFullName
            };

            BamlAssemblyInfoRecord bamlAssemblyInfoRecord = (BamlAssemblyInfoRecord)GetHashTableData(key);

            if (bamlAssemblyInfoRecord is null)
            {
                bamlAssemblyInfoRecord = new BamlAssemblyInfoRecord
                {
                    AssemblyFullName = assemblyFullName
                };

#if PBTCOMPILER
                try
                {
                    if (bamlAssemblyInfoRecord.Assembly == null)
                    {
                        // Load the assembly so that we can get the Assembly.FullName to store in Baml.
                        // This will ensure that we are we use the same assembly during compilation and loading.
                        GetAssemblyFromAssemblyInfo(bamlAssemblyInfoRecord);
                        if (bamlAssemblyInfoRecord.Assembly != null &&
                            bamlAssemblyInfoRecord.Assembly.FullName != assemblyFullName)
                        {
                            // Given name is a partial name for the assembly

                            // Add AssemblyInfo for the full name of the assembly and add a cache entry
                            // for the same record against the partial name. Note that there is no need
                            // to write a Baml record for the partial assembly name
                            bamlAssemblyInfoRecord = AddAssemblyMap(binaryWriter, bamlAssemblyInfoRecord.Assembly.FullName);

                            ObjectHashTable.Add(key, bamlAssemblyInfoRecord);

                            // Records written out the Baml must have a legitimate Assembly ID
                            Debug.Assert(bamlAssemblyInfoRecord.AssemblyId >= 0);

                            return bamlAssemblyInfoRecord;
                        }
                        else
                        {
                            // Given name is the full name for the assembly. Simply add a cache entry for
                            // the record and write it out to Baml.
                        }
                    }
                }
                catch (Exception e)
                {
                    if (CriticalExceptions.IsCriticalException(e))
                    {
                        throw;
                    }

                    // It is possible that the we are writing out a record for the very same assembly
                    // that is being built on compilation. Hence the assembly may not get loaded.
                }
#endif

                // review, could be a race condition here.
                AssemblyIdMap.Add(bamlAssemblyInfoRecord);
                bamlAssemblyInfoRecord.AssemblyId = (short)(AssemblyIdMap.Count - 1);

                ObjectHashTable.Add(key, bamlAssemblyInfoRecord);

                // Write to BAML
                bamlAssemblyInfoRecord.Write(binaryWriter);
            }
            else if (bamlAssemblyInfoRecord.AssemblyId == -1)
            {
                // This is the case when EnsureAssemblyRecord has cached the AssemblyInfo but hasn't
                // written it out to Baml. This now needs to be written out to Baml.

                Debug.Assert(
                    bamlAssemblyInfoRecord.Assembly != null &&
                    bamlAssemblyInfoRecord.Assembly.FullName == bamlAssemblyInfoRecord.AssemblyFullName);

                // review, could be a race condition here.
                AssemblyIdMap.Add(bamlAssemblyInfoRecord);
                bamlAssemblyInfoRecord.AssemblyId = (short)(AssemblyIdMap.Count - 1);

                // Write to BAML
                bamlAssemblyInfoRecord.Write(binaryWriter);
            }

            // Records written out the Baml must have a legitimate Assembly ID
            Debug.Assert(bamlAssemblyInfoRecord.AssemblyId >= 0);

            return bamlAssemblyInfoRecord;
        }

#if !PBTCOMPILER
        // Add an already-loaded assembly record to the map table.  These are generally
        // appended to the end.  If we specify an existing slot in the table, make
        // sure we are not overwriting existing data.
        internal void LoadAssemblyInfoRecord(BamlAssemblyInfoRecord record)
        {
            Debug.Assert(AssemblyIdMap.Count == record.AssemblyId || record.AssemblyFullName == AssemblyIdMap[record.AssemblyId].AssemblyFullName);

            if (AssemblyIdMap.Count == record.AssemblyId)
            {
                AssemblyIdMap.Add(record);
            }
        }
#endif

        /// <summary>
        /// Ensure we have an assembly record for this assembly
        /// </summary>
        internal void EnsureAssemblyRecord(Assembly asm)
        {
            string fullName = asm.FullName;

            // If we don't have an assembly record for this assembly yet it is most likely
            // because it is an assembly that is part of the default namespace and was not defined
            // using a mapping PI.  In that case, add an assembly record to the object cache and
            // populate it with the required data.  Note that it DOES NOT have a valid AssemblyId
            // and this is not written out the the baml stream.
            if (ObjectHashTable[fullName] is not BamlAssemblyInfoRecord record)
            {
                record = new BamlAssemblyInfoRecord
                {
                    AssemblyFullName = fullName,
                    Assembly = asm
                };
                ObjectHashTable[fullName] = record;
            }

        }

        // Return the hash table key used for a type info record with the given
        // assembly name and type full name.  The type full name must include
        // the entire clr namespace.
        private TypeInfoKey GetTypeInfoKey(
                               string assemblyFullName,
                               string typeFullName)
        {
            TypeInfoKey key = new TypeInfoKey
            {
                DeclaringAssembly = assemblyFullName,
                TypeFullName = typeFullName
            };
            return key;
        }

        // Given an assembly name and a type name, see if there is a known fixed type
        // that corresponds to this, or if we have already added a type info record for
        // this type.  If so, set the returned id and answer true.  Otherwise answer false.
        internal bool GetTypeInfoId(
                             BinaryWriter binaryWriter,
                             string assemblyFullName,
                             string typeFullName,
                         out short typeId)
        {
            int dotIndex = typeFullName.LastIndexOf('.');
            string typeShortName;
            string typeClrNamespace;
            if (dotIndex >= 0)
            {
                typeShortName = typeFullName.Substring(dotIndex + 1);
                typeClrNamespace = typeFullName.Substring(0, dotIndex);
            }
            else
            {
                typeShortName = typeFullName;
                typeClrNamespace = string.Empty;
            }
            typeId = GetKnownTypeIdFromName(assemblyFullName, typeClrNamespace, typeShortName);

            if (typeId < 0)
            {
                return true;
            }

            TypeInfoKey key = GetTypeInfoKey(assemblyFullName, typeFullName);

            BamlTypeInfoRecord bamlTypeInfoRecord = (BamlTypeInfoRecord)GetHashTableData(key);

            if (bamlTypeInfoRecord == null)
            {
                return false;
            }
            else
            {
                typeId = bamlTypeInfoRecord.TypeId;
                return true;
            }
        }


        // Return the Type ID for the passed typeFullName by creating a new record and adding
        // it to the type info hashtable.  A BamlTypeInfoRecord is created if there is no serializer
        // for this type, and a BamlTypeInfoWithSerializerRecord is created if there is a serializer.
        internal short AddTypeInfoMap(BinaryWriter binaryWriter,
                                      string assemblyFullName,
                                      string typeFullName,
                                      Type elementType,
                                      string serializerAssemblyFullName,
                                      string serializerTypeFullName)
        {
            TypeInfoKey key = GetTypeInfoKey(assemblyFullName, typeFullName);
            BamlTypeInfoRecord bamlTypeInfoRecord;

            // Create either a normal TypeInfo record, or a TypeInfoWithSerializer record, depending
            // on whether or not there is a serializer for this type.
            if (serializerTypeFullName == string.Empty)
            {
                bamlTypeInfoRecord = new BamlTypeInfoRecord();
            }
            else
            {
                bamlTypeInfoRecord = new BamlTypeInfoWithSerializerRecord();
                short serializerTypeId;
                // If we do not already have a type record for the serializer associated with
                // this type, then recurse and write out the serializer assembly and type first.
                if (!GetTypeInfoId(binaryWriter, serializerAssemblyFullName, serializerTypeFullName, out serializerTypeId))
                {
                    serializerTypeId = AddTypeInfoMap(binaryWriter, serializerAssemblyFullName,
                                                      serializerTypeFullName, null,
                                                      string.Empty, string.Empty);
                }
                ((BamlTypeInfoWithSerializerRecord)bamlTypeInfoRecord).SerializerTypeId = serializerTypeId;
            }

            bamlTypeInfoRecord.TypeFullName = typeFullName;

            // get id for assembly
            BamlAssemblyInfoRecord bamlAssemblyInfoRecord = AddAssemblyMap(binaryWriter, assemblyFullName);
            bamlTypeInfoRecord.AssemblyId = bamlAssemblyInfoRecord.AssemblyId;

            bamlTypeInfoRecord.IsInternalType = (elementType != null && ReflectionHelper.IsInternalType(elementType));

            // review, could be a race condition here.
            TypeIdMap.Add(bamlTypeInfoRecord);
            bamlTypeInfoRecord.TypeId = (short)(TypeIdMap.Count - 1);

            // add to the hash
            ObjectHashTable.Add(key, bamlTypeInfoRecord);

            // Write to BAML
            bamlTypeInfoRecord.Write(binaryWriter);

            return bamlTypeInfoRecord.TypeId;
        }

#if !PBTCOMPILER
        // Add an already-loaded type info record to the map table.  This can
        // only be appended to the end of the existing map.  If it is the same
        // as an existing record, ensure we're not trying to overwrite something.
        internal void LoadTypeInfoRecord(BamlTypeInfoRecord record)
        {
            Debug.Assert(TypeIdMap.Count == record.TypeId || record.TypeFullName == TypeIdMap[record.TypeId].TypeFullName);

            if (TypeIdMap.Count == record.TypeId)
            {
                TypeIdMap.Add(record);
            }
        }
#endif

        // Return the key to use when inserting or extracting attribute information
        // from the object cache.
        internal object GetAttributeInfoKey(
            string ownerTypeName,
            string attributeName)
        {
            Debug.Assert(ownerTypeName != null);
            Debug.Assert(attributeName != null);
            return $"{ownerTypeName}.{attributeName}";
        }

        // Return the attribute info record that corresponds to the passed type and field name.
        // If one does not already exist, then create a new one.  This can involve first creating
        // a type info record that corresponds to the owner type and the attribute type.
        // This method also checks if the attributeType is custom serializable or convertible.
        internal short AddAttributeInfoMap(
            BinaryWriter binaryWriter,
            string assemblyFullName,                // Name of assembly for owning or declaring type
            string typeFullName,                    // Type name of object that owns or declares this attribute
            Type owningType,                      // Actual type of the object the owns or declares this attribute
            string fieldName,                       // Name of the attribute
            Type attributeType,                     // Type of the attribute or property itself; not its owner type
            BamlAttributeUsage attributeUsage)      // Special flags for how this attribute is used.
        {
            BamlAttributeInfoRecord record;
            return AddAttributeInfoMap(binaryWriter, assemblyFullName, typeFullName, owningType, fieldName, attributeType, attributeUsage, out record);
        }

        internal short AddAttributeInfoMap(
            BinaryWriter binaryWriter,
            string assemblyFullName,                // Name of assembly for owning or declaring type
            string typeFullName,                    // Type name of object that owns or declares this attribute
            Type owningType,                        // Actual type of the object the owns or declares this attribute
            string fieldName,                       // Name of the attribute
            Type attributeType,                     // Type of the attribute or property itself; not its owner type
            BamlAttributeUsage attributeUsage,      // Special flags for how this attribute is used.
            out BamlAttributeInfoRecord bamlAttributeInfoRecord)     // Record if not a known DP
        {
            short typeId;
            // If we do not already have a type record for the type associated with
            // this attribute, then recurse and write out the TypeInfo first.
            if (!GetTypeInfoId(binaryWriter, assemblyFullName, typeFullName, out typeId))
            {
                // Get id for Type that owns or has this attribute declared on it.  This is
                // refered to in the attribute info record and is also part of the key for
                // seeing if we already have an attribute info record for this attribute.
                Type serializerType = XamlTypeMapper.GetXamlSerializerForType(owningType);
                string serializerTypeAssemblyName = serializerType == null ?
                                         string.Empty : serializerType.Assembly.FullName;
                string serializerTypeName = serializerType == null ?
                                         string.Empty : serializerType.FullName;

                typeId = AddTypeInfoMap(binaryWriter, assemblyFullName, typeFullName,
                                        owningType, serializerTypeAssemblyName,
                                        serializerTypeName);
            }
            else if (typeId < 0)
            {
                // Only known types will have known properties
                short attributeId = (short)-KnownTypes.GetKnownPropertyAttributeId((KnownElements)(-typeId), fieldName);
                if (attributeId < 0)
                {
                    // The property is known and doesn't need a record created.
                    bamlAttributeInfoRecord = null;
                    return attributeId;
                }
            }

            object key = GetAttributeInfoKey(typeFullName, fieldName);

            bamlAttributeInfoRecord = (BamlAttributeInfoRecord)GetHashTableData(key);
            if (bamlAttributeInfoRecord is null)
            {
                // The property is new and needs a record created.
                bamlAttributeInfoRecord = new BamlAttributeInfoRecord
                {
                    Name = fieldName,
                    OwnerTypeId = typeId
                };

                AttributeIdMap.Add(bamlAttributeInfoRecord);
                bamlAttributeInfoRecord.AttributeId = (short)(AttributeIdMap.Count - 1);
                bamlAttributeInfoRecord.AttributeUsage = attributeUsage;

                // add to the hash
                ObjectHashTable.Add(key, bamlAttributeInfoRecord);

                // Write to BAML
                bamlAttributeInfoRecord.Write(binaryWriter);
            }

            return bamlAttributeInfoRecord.AttributeId;
        }

        // Set flags saying whether this attribute supports custom serialization or type conversion,
        // based on whether the attribute type has a serializer or a type converter.
        // returns true if a serialiazer was found, else if a converter or nothing was found, returns false.
        internal bool GetCustomSerializerOrConverter(
                BinaryWriter binaryWriter,
                Type ownerType,             // Type of object that owns or declares this attribute
                Type attributeType,         // Type of the attribute or property itself; not its owner type
                object piOrMi,                // PropertyInfo or AttachedPropertySetter corresponding to the attribute
                string fieldName,             // Name of the property
            out short converterOrSerializerTypeId,
            out Type converterOrSerializerType)
        {
            converterOrSerializerType = null;
            converterOrSerializerTypeId = 0;

            if (!ShouldBypassCustomCheck(ownerType, attributeType))
            {
                converterOrSerializerType = GetCustomSerializer(attributeType, out converterOrSerializerTypeId);

                // NOTE: We do not want to check for custom converters when the property is
                // known to be custom serializable or it is a complex property.
                if (converterOrSerializerType != null)
                {
                    return true;
                }

                converterOrSerializerType = GetCustomConverter(piOrMi, ownerType, fieldName, attributeType);

                // Enum prop values are custom serilized if there is no custom convertor for them.
                // This needs to be done here as GetCustomSerilaizer() does not do this in order to
                // give precedence to custom TypeConverters first.
                if (converterOrSerializerType == null && attributeType.IsEnum)
                {
                    converterOrSerializerTypeId = (short)KnownElements.EnumConverter;
                    converterOrSerializerType = KnownTypes.Types[(int)converterOrSerializerTypeId];
                    return true;
                }

                // If we found a custom serializer or a type converter then we need to write it out to Baml
                if (converterOrSerializerType != null)
                {
                    string converterOrSerializerTypeFullName = converterOrSerializerType.FullName;

                    EnsureAssemblyRecord(converterOrSerializerType.Assembly);

                    // If we do not already have a type record for the type associated with with the serializer or
                    // type converter for this attribute, then recurse and write out the TypeInfo first.
                    // NOTE: Known types will get filtered out here.
                    if (!GetTypeInfoId(binaryWriter, converterOrSerializerType.Assembly.FullName,
                                       converterOrSerializerTypeFullName, out converterOrSerializerTypeId))
                    {
                        // Get id for the converter or serializer type.
                        converterOrSerializerTypeId = AddTypeInfoMap(binaryWriter,
                                            converterOrSerializerType.Assembly.FullName,
                                            converterOrSerializerTypeFullName,
                                            null, string.Empty, string.Empty);
                    }
                }
            }

            return false;
        }

        // Given a string value, see if there is a known fixed string value
        // that corresponds to this, or if we have already added a string info record for
        // this value.  If so, set the returned id and answer true.  Otherwise answer false.
        internal bool GetStringInfoId(
                 string stringValue,
             out Int16 stringId)
        {
            stringId = GetKnownStringIdFromName(stringValue);

            if (stringId < 0)
            {
                return true;
            }

            BamlStringInfoRecord bamlStringInfoRecord = (BamlStringInfoRecord)GetHashTableData(stringValue);

            if (bamlStringInfoRecord == null)
            {
                return false;
            }
            else
            {
                stringId = bamlStringInfoRecord.StringId;
                return true;
            }
        }

        // String table Map
        // This adds a new BamlStringInfoRecord to the string table for
        // a given string.  This should NOT be called if the string already
        // is in the string table and in the known objects hash table.
        internal Int16 AddStringInfoMap(
            BinaryWriter binaryWriter,
            string stringValue)
        {
            Debug.Assert(GetHashTableData(stringValue) == null,
                "Already have this String in the BamlMapTable string table");

            BamlStringInfoRecord stringInfo = new();
            StringIdMap.Add(stringInfo);
            stringInfo.StringId = (short)(StringIdMap.Count - 1);
            stringInfo.Value = stringValue;

            // add to the hash
            ObjectHashTable.Add(stringValue, stringInfo);

            // Write to BAML
            stringInfo.Write(binaryWriter);

            // return the string id
            return stringInfo.StringId;
        }

        // Returns an Id for a static property or field member whose Type could be
        // a known SystemResourceKey or DepenencyProperty or other custom value.
        internal short GetStaticMemberId(BinaryWriter binaryWriter,
                                         ParserContext pc,
                                         short extensionTypeId,
                                         string memberValue,
                                         Type defaultTargetType)
        {
            short memberId = 0;
            Type targetType = null;
            string memberName = null;

            switch (extensionTypeId)
            {
                case (short)KnownElements.StaticExtension:

                    targetType = XamlTypeMapper.GetTargetTypeAndMember(memberValue, pc, true, out memberName);
                    Debug.Assert(targetType != null && memberName != null);

                    MemberInfo memberInfo = XamlTypeMapper.GetStaticMemberInfo(targetType, memberName, false);
                    if (memberInfo is PropertyInfo)
                    {
                        // see if the value is a static SystemResourceKey property from the themes
                        memberId = SystemResourceKey.GetBamlIdBasedOnSystemResourceKeyId(targetType, memberName);
                    }

                    break;

                case (short)KnownElements.TemplateBindingExtension:
                    targetType = XamlTypeMapper.GetDependencyPropertyOwnerAndName(memberValue,
                                                                                  pc,
                                                                                  defaultTargetType,
                                                                              out memberName);
                    break;
            }

            if (memberId == 0)
            {
                // if not a known member, add it to the attributeInfo Map.
                // This will automatically account for known DPs.
                memberId = AddAttributeInfoMap(binaryWriter,
                                               targetType.Assembly.FullName,
                                               targetType.FullName,
                                               targetType,
                                               memberName,
                                               null,
                                               BamlAttributeUsage.Default);
            }

            return memberId;
        }

        // Particular attributes do need to be tested for custom serialization
        // or type conversion. This method checks for those.
        private bool ShouldBypassCustomCheck(
            Type declaringType,  // Type of object that owns or declares this attribute
            Type attributeType) // Type of the attribute or property itself; not its owner type
        {
            // declaringType could be null/empty for some Style properties such as PropertyPath/Value/Target.
            if (declaringType == null)
            {
                return true;
            }

            // attributeType could be null for IList/IDictionary/Array complex properties or for events
            if (attributeType == null)
            {
                return true;
            }

            return false;
        }

        // Returns a TypeConverter for a property.
        private Type GetCustomConverter(
                object piOrMi,             // PropertyInfo or AttachedPropertySetter for the attribute
                Type ownerType,          // Type of object that owns or declares this attribute
                string fieldName,          // Name of the attribute
                Type attributeType)      // Type of the attribute or property itself; not its owner type
        {
            // Check for known per property type converter
            Type converterType = GetKnownConverterTypeFromPropName(ownerType, fieldName);
            if (converterType != null)
            {
                return converterType;
            }

            // Reflect for per property type converter , but skip if WinFX props
            Assembly ownerAsm = ownerType.Assembly;
#if PBTCOMPILER
            if (XamlTypeMapper.AssemblyPF != ownerAsm &&
                XamlTypeMapper.AssemblyPC != ownerAsm &&
                XamlTypeMapper.AssemblyWB != ownerAsm)
#else
            if (!ownerAsm.FullName.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) &&
                !ownerAsm.FullName.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase) &&
                !ownerAsm.FullName.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase))
#endif
            {
                converterType = XamlTypeMapper.GetPropertyConverterType(attributeType, piOrMi);
                if (converterType != null)
                {
                    return converterType;
                }
            }

            // If we haven't found a per-property converter, look for the converter
            // for the attribute's type.
            converterType = XamlTypeMapper.GetTypeConverterType(attributeType);

            return converterType;
        }

        // Returns a custom serializer Type & Id that is used to more
        // efficiently store the value of a "type" instance in binary format
        private Type GetCustomSerializer(
                Type type,
            out short converterOrSerializerTypeId)
        {
            int index;

            // Check for known custom serializable types
            if (type == typeof(Boolean))
            {
                index = (int)KnownElements.BooleanConverter;
            }
            else if (type == KnownTypes.Types[(int)KnownElements.DependencyProperty])
            {
                index = (int)KnownElements.DependencyPropertyConverter;
            }
            else
            {
                index = XamlTypeMapper.GetCustomBamlSerializerIdForType(type);
                if (index == 0)
                {
                    converterOrSerializerTypeId = 0;
                    return null;
                }
            }

            converterOrSerializerTypeId = (short)index;
            return KnownTypes.Types[index];
        }

#if !PBTCOMPILER
        // Helper method to throw an Exception.
        [DoesNotReturn]
        private static void ThrowException(string id, string parameter)
        {
            ApplicationException bamlException = new(SR.Format(SR.GetResourceString(id), parameter));

            throw bamlException;
        }

        // Add an already-loaded type info record to the map table.  This is generally
        // appended as the baml file is being built.  If we are loading an attribute
        // into an already set up map table, just ensure that we don't try to overwrite
        // an existing record something different.
        internal void LoadAttributeInfoRecord(BamlAttributeInfoRecord record)
        {
            Debug.Assert(AttributeIdMap.Count == record.AttributeId || record.Name == AttributeIdMap[record.AttributeId].Name);

            if (AttributeIdMap.Count == record.AttributeId)
            {
                AttributeIdMap.Add(record);
            }
        }

        // Add an already-loaded String info record to the map table.  This is generally
        // appended as the baml file is being built.  If we are loading a string
        // into an already set up map table, just ensure that we don't try to overwrite
        // an existing record something different.
        internal void LoadStringInfoRecord(BamlStringInfoRecord record)
        {
            Debug.Assert(StringIdMap.Count == record.StringId || record.Value == StringIdMap[record.StringId].Value);

            if (StringIdMap.Count == record.StringId)
            {
                StringIdMap.Add(record);
            }
        }
#endif

        // returns the items if found in the hash table
        // if no item is found null is returned.
        internal Object GetHashTableData(object key)
        {
            return ObjectHashTable[key];
        }

#if !PBTCOMPILER
        // Add item to the hash table.  Only do this if the map table
        // is being re-used for multiple parses.  Otherwise the hash table
        // data is of no use for a single parse.
        internal void AddHashTableData(object key, object data)
        {
            if (_reusingMapTable)
            {
                ObjectHashTable[key] = data;
            }
        }
#endif

#if !PBTCOMPILER
        internal BamlMapTable Clone() => new BamlMapTable(_xamlTypeMapper)
        {
            ObjectHashTable = (Hashtable)_objectHashTable.Clone(),
            AssemblyIdMap = new List<BamlAssemblyInfoRecord>(_assemblyIdToInfo),
            TypeIdMap = new List<BamlTypeInfoRecord>(_typeIdToInfo),
            AttributeIdMap = new List<BamlAttributeInfoRecord>(_attributeIdToInfo),
            StringIdMap = new List<BamlStringInfoRecord>(_stringIdToInfo)
        };

        private TypeConverter GetConverterFromCache(short typeId)
        {
            TypeConverter tc = null;
            if (_converterCache != null)
            {
                tc = _converterCache[typeId] as TypeConverter;
            }

            return tc;
        }

        private TypeConverter GetConverterFromCache(Type type)
        {
            TypeConverter tc = null;
            if (_converterCache != null)
            {
                tc = _converterCache[type] as TypeConverter;
            }

            return tc;
        }

        internal void ClearConverterCache()
        {
            // clear the Type converterCache to reduce survived memory allocs
            _converterCache?.Clear();
            _converterCache = null;
        }
#endif

        #endregion Methods

        #region Properties

        private Hashtable ObjectHashTable
        {
            get => _objectHashTable;
#if !PBTCOMPILER
            init => _objectHashTable = value;
#endif
        }

        private List<BamlAssemblyInfoRecord> AssemblyIdMap
        {
            get => _assemblyIdToInfo;
#if !PBTCOMPILER
            init => _assemblyIdToInfo = value;
#endif
        }

        private List<BamlTypeInfoRecord> TypeIdMap
        {
            get => _typeIdToInfo;
#if !PBTCOMPILER
            init => _typeIdToInfo = value;
#endif
        }

        private List<BamlAttributeInfoRecord> AttributeIdMap
        {
            get => _attributeIdToInfo;
#if !PBTCOMPILER
            init => _attributeIdToInfo = value;
#endif
        }

        private List<BamlStringInfoRecord> StringIdMap
        {
            get => _stringIdToInfo;
#if !PBTCOMPILER
            init => _stringIdToInfo = value;
#endif
        }

        internal XamlTypeMapper XamlTypeMapper
        {
            get => _xamlTypeMapper;
#if !PBTCOMPILER
            set => _xamlTypeMapper = value;
#endif
        }

        // Return true if this map table has been sealed, meaning it contains a complete
        // set of attribute information for a particular baml file.
#if !PBTCOMPILER
        private Hashtable ConverterCache
        {
            get
            {
                if (_converterCache == null)
                {
                    _converterCache = new Hashtable();
                }

                return _converterCache;
            }
        }
#endif

        #endregion Properties


        #region Data

        private const string _coreAssembly = "PresentationCore";
        private const string _frameworkAssembly = "PresentationFramework";

        private static string[] _knownStrings =
        {
            null,
            "Name",
            "Uid",
        };

        internal static short NameStringId = -1;     // Known index of "Name"
        internal static short UidStringId = -2;     // Known index of "Uid"
        internal static string NameString = "Name"; // Now used during Style serialization

        // currently one Hastable for everything, when do perf
        // see if any advantage in breaking up or searching
        // by PropId, etc. instead of Hash. Hash is only used on compile
        // so leave null so Load doesn't take the hit.
        private readonly Hashtable _objectHashTable = new();

        /// <summary>
        /// List of <see cref="BamlAssemblyInfoRecord"/> (user assemblies).
        /// </summary>
        private readonly List<BamlAssemblyInfoRecord> _assemblyIdToInfo = new();
        /// <summary>
        /// List of <see cref="BamlTypeInfoRecord"/> (class types).
        /// </summary>
        private readonly List<BamlTypeInfoRecord> _typeIdToInfo = new();
        /// <summary>
        /// List of <see cref="BamlAttributeInfoRecord"/> (attribute IDs).
        /// </summary>
        private readonly List<BamlAttributeInfoRecord> _attributeIdToInfo = new(10);
        /// <summary>
        /// List of <see cref="BamlStringInfoRecord"/> (strings).
        /// </summary>
        private readonly List<BamlStringInfoRecord> _stringIdToInfo = new();

        /// <summary>
        /// XamlTypeMapper associated with this map table. There is always a one-to-one correspondence.
        /// </summary>
        private XamlTypeMapper _xamlTypeMapper;

        // The assembly record for the known types of controls
        private BamlAssemblyInfoRecord _knownAssemblyInfoRecord;

#if !PBTCOMPILER
        // Temporary cache of Known Type Converters for each baml reading session.
        private Hashtable _converterCache = null;
#endif

#if !PBTCOMPILER
        // True if this instance of the BamlMapTable is being reused between
        // different parses.  This is done to maintain the ObjectHashTable so that
        // less reflection is done for types and properties.
        private bool   _reusingMapTable = false;
#endif

        #endregion Data

    }

    // Key object for hash table of assemblies.  This is keyed by assembly full name.
    internal struct AssemblyInfoKey
    {
        internal string AssemblyFullName;

        /// <summary>
        ///     Determines if the passed in struct is equal to this struct.
        ///     Two keys will be equal if they both have equal assembly names.
        /// </summary>
        public override bool Equals(object o)
        {
            if (o is AssemblyInfoKey key)
            {
                return ((key.AssemblyFullName != null) ?
                                      key.AssemblyFullName.Equals(this.AssemblyFullName) :
                                      (this.AssemblyFullName == null));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///     Forward to .Equals
        /// </summary>
        public static bool operator ==(AssemblyInfoKey key1, AssemblyInfoKey key2)
        {
            return key1.Equals(key2);
        }

        /// <summary>
        ///     Forward to .Equals
        /// </summary>
        public static bool operator !=(AssemblyInfoKey key1, AssemblyInfoKey key2)
        {
            return !(key1.Equals(key2));
        }

        /// <summary>
        ///     Serves as a hash function for using this in a hashtable.
        /// </summary>
        public override int GetHashCode()
        {
            return ((AssemblyFullName != null) ? AssemblyFullName.GetHashCode() : 0);
        }

        /// <summary>
        ///     Return string representation of this key
        /// </summary>
        public override string ToString()
        {
            return AssemblyFullName;
        }

    }


    // Key object for hash table of types.  This is keyed by assembly and
    // type full name.
    internal struct TypeInfoKey
    {
        internal string DeclaringAssembly;
        internal string TypeFullName;

        /// <summary>
        ///     Determines if the passed in struct is equal to this struct.
        ///     Two keys will be equal if they both have equal assembly and names.
        /// </summary>
        public override bool Equals(object o)
        {
            if (o is TypeInfoKey key)
            {
                return ((key.DeclaringAssembly != null) ?
                                      key.DeclaringAssembly.Equals(this.DeclaringAssembly) :
                                      (this.DeclaringAssembly == null)) &&
                       ((key.TypeFullName != null) ?
                                      key.TypeFullName.Equals(this.TypeFullName) :
                                      (this.TypeFullName == null));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///     Forward to .Equals
        /// </summary>
        public static bool operator ==(TypeInfoKey key1, TypeInfoKey key2)
        {
            return key1.Equals(key2);
        }

        /// <summary>
        ///     Forward to .Equals
        /// </summary>
        public static bool operator !=(TypeInfoKey key1, TypeInfoKey key2)
        {
            return !(key1.Equals(key2));
        }

        /// <summary>
        ///     Serves as a hash function for using this in a hashtable.
        /// </summary>
        public override int GetHashCode()
        {
            return ((DeclaringAssembly != null) ? DeclaringAssembly.GetHashCode() : 0) ^
                    ((TypeFullName != null) ? TypeFullName.GetHashCode() : 0);
        }

        /// <summary>
        ///     Return string representation of this key
        /// </summary>
        public override string ToString() =>
            $"TypeInfoKey: Assembly={(DeclaringAssembly ?? "null")} Type={(TypeFullName ?? "null")}";
    }
}
