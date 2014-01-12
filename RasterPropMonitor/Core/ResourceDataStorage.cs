using System.Collections.Generic;
using System;

namespace JSI
{
	public class ResourceDataStorage
	{
		// Mihara: This is rather bad code. Maybe when I'm not feeling so stupid, I'll rewrite it.
		private readonly Dictionary<string,int> nameID = new Dictionary<string, int>();
		private readonly Dictionary<int,ResourceData> data = new Dictionary<int,ResourceData>();
		private double lastcheck;
		private const double secondsBetweenSamples = 1;

		public ResourceDataStorage()
		{
			foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions) {
				data.Add(thatResource.id, new ResourceData());
				data[thatResource.id].density = thatResource.density;
				nameID.Add(thatResource.name, thatResource.id);
			}

		}

		public void StartLoop(double time)
		{

			foreach (KeyValuePair<int, ResourceData> resource in data) {
				if (time - lastcheck > secondsBetweenSamples) {
					data[resource.Key].delta = (data[resource.Key].previous - data[resource.Key].current) / (time - lastcheck);
					data[resource.Key].previous = data[resource.Key].current;
				}
				data[resource.Key].current = 0;
				data[resource.Key].max = 0;
				data[resource.Key].stage = 0;
				data[resource.Key].stagemax = 0;
			}

			if (time - lastcheck > secondsBetweenSamples) {
				lastcheck = time;
			}
		}

		public string[] Alphabetic()
		{
			var names = new List<string>();
			foreach (KeyValuePair<string,int> resource in nameID) {
				if (data[resource.Value].max > 0) {
					names.Add(resource.Key);
				}
			}
			var result = names.ToArray();
			Array.Sort(result);
			return result;
		}

		public object ListElement(string resourceName, string valueType, double time, bool stage)
		{
			if (!nameID.ContainsKey(resourceName))
				return 0d;
			switch (valueType) {
				case "":
				case "VAL":
					return stage ? data[nameID[resourceName]].stage : data[nameID[resourceName]].current;
				case "DENSITY":
					return data[nameID[resourceName]].density;
				case "DELTA":
					return data[nameID[resourceName]].delta;
				case "MASS":
					return data[nameID[resourceName]].density * (stage ? data[nameID[resourceName]].stage : data[nameID[resourceName]].current);
				case "MAXMASS":
					return data[nameID[resourceName]].density * (stage ? data[nameID[resourceName]].stagemax : data[nameID[resourceName]].max);
				case "MAX":
					return stage ? data[nameID[resourceName]].stagemax : data[nameID[resourceName]].max;
				case "PERCENT":
					if (stage)
						return  data[nameID[resourceName]].stagemax > 0 ? data[nameID[resourceName]].stage / data[nameID[resourceName]].stagemax : 0d;
					return  data[nameID[resourceName]].max > 0 ? data[nameID[resourceName]].current / data[nameID[resourceName]].max : 0d;
			}
			return 0d;
		}

		public void Add(PartResource resource)
		{
			data[resource.info.id].current += resource.amount;
			data[resource.info.id].max += resource.maxAmount;
		}

		public void SetActive(Vessel.ActiveResource resource)
		{
			data[resource.info.id].stage = resource.amount;
			data[resource.info.id].stagemax = resource.maxAmount;
		}

		private class ResourceData
		{
			public double current;
			public double max;
			public double previous;
			public double stage;
			public double stagemax;
			public double density;
			public double delta;
		}
	}
}
