﻿using Newtonsoft.Json;

namespace Nest
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	[ContractJsonConverter(typeof(ReadAsTypeJsonConverter<MaxBucketAggregation>))]
	public interface IMaxBucketAggregation : IPipelineAggregation { }

	public class MaxBucketAggregation
		: PipelineAggregationBase, IMaxBucketAggregation
	{
		public override string TypeName => "max_bucket";

		internal MaxBucketAggregation () { }

		public MaxBucketAggregation(string name, SingleBucketsPath bucketsPath)
			: base(name, bucketsPath) { }

		internal override void WrapInContainer(AggregationContainer c) => c.MaxBucket = this;
	}

	public class MaxBucketAggregationDescriptor
		: PipelineAggregationDescriptorBase<MaxBucketAggregationDescriptor, IMaxBucketAggregation, SingleBucketsPath>
		, IMaxBucketAggregation
	{
		public override string TypeName => "max_bucket";
	}
}
