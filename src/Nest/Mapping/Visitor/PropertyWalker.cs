﻿using Nest.Resolvers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Nest
{
	public class PropertyWalker
	{
		private Type _type;
		private IPropertyVisitor _visitor;
		private int _maxRecursion;
		private ConcurrentDictionary<Type, int> _seenTypes;

		public PropertyWalker(Type type, int maxRecursion = 0) : this(type, null, maxRecursion) { }

		public PropertyWalker(Type type, IPropertyVisitor visitor, int maxRecursion = 0)
		{
			_type = GetUnderlyingType(type);
			_visitor = visitor ?? new NoopPropertyVisitor();
			_maxRecursion = maxRecursion;
			_seenTypes = new ConcurrentDictionary<Type, int>();
			_seenTypes.TryAdd(_type, 0);
		}

		internal PropertyWalker(Type type, IPropertyVisitor visitor, int maxRecursion, ConcurrentDictionary<Type, int> seenTypes)
		{
			_type = type;
			_visitor = visitor;
			_maxRecursion = maxRecursion;
			_seenTypes = seenTypes;
		}

		public IProperties GetProperties()
		{
			var properties = new Properties();

			int seen;
			if (_seenTypes.TryGetValue(_type, out seen) && seen > _maxRecursion)
				return properties;

			foreach(var propertyInfo in _type.GetProperties())
			{
				var attribute = ElasticsearchPropertyAttribute.From(propertyInfo);
				if (attribute != null && attribute.Ignore)
					continue;
				var property = GetProperty(propertyInfo, attribute);
				properties.Add(propertyInfo.Name, property);
			}

			return properties;
		}

		private IElasticsearchProperty GetProperty(PropertyInfo propertyInfo, ElasticsearchPropertyAttribute attribute)
		{
			var elasticType = GetElasticType(propertyInfo, attribute);
			var objectType = elasticType as IObjectProperty;
			if (objectType != null)
			{
				var type = GetUnderlyingType(propertyInfo.PropertyType);
				var seenTypes = new ConcurrentDictionary<Type, int>(_seenTypes);
				seenTypes.AddOrUpdate(type, 0, (t, i) => ++i);
				var walker = new PropertyWalker(type, _visitor, _maxRecursion, seenTypes);
				objectType.Properties = walker.GetProperties();
			}
			_visitor.Visit(elasticType, propertyInfo, attribute);
			return elasticType;	
		}

		private IElasticsearchProperty GetElasticType(PropertyInfo propertyInfo, ElasticsearchPropertyAttribute attribute)
		{
			var elasticType = _visitor.Visit(propertyInfo, attribute);
			if (elasticType != null)
				return elasticType;
			return (attribute != null) 
				? attribute.ToProperty() 
				: InferElasticType(propertyInfo.PropertyType);
		}

		private IElasticsearchProperty InferElasticType(Type type)
		{
			type = GetUnderlyingType(type);

			if (type == typeof(string))
				return new StringProperty();

			if (type.IsEnum)
				return new NumberProperty(NumberType.Integer);

			if (type.IsValueType)
			{
				switch (type.Name)
				{
					case "Int32":
					case "UInt32":
						return new NumberProperty(NumberType.Integer);
					case "Int16":
					case "UInt16":
						return new NumberProperty(NumberType.Short);
					case "Byte":
					case "SByte":
						return new NumberProperty(NumberType.Byte);
					case "Int64":
					case "UInt64":
						return new NumberProperty(NumberType.Long);
					case "Single":
						return new NumberProperty(NumberType.Float);
					case "Decimal":
					case "Double":
						return new NumberProperty(NumberType.Double);
					case "DateTime":
						return new DateProperty();
					case "Boolean":
						return new BooleanProperty();
				}
			}
			
			return new ObjectProperty();
		}

		private Type GetUnderlyingType(Type type)
		{
			if (type.IsArray)
				return type.GetElementType();

			if (type.IsGenericType && type.GetGenericArguments().Length == 1 && (type.GetInterface("IEnumerable") != null || Nullable.GetUnderlyingType(type) != null))
				return type.GetGenericArguments()[0];

			return type;
		}
	}
}