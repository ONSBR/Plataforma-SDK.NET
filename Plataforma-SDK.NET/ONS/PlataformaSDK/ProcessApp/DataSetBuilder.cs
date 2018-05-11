using System;
using ONS.PlataformaSDK.Entities;
using System.IO;
using YamlDotNet.Serialization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ONS.PlataformaSDK.ProcessApp
{
    public class DataSetBuilder
    {
        public List<EntityFilter> EntitiesFilters;
        private Object Payload;

        public DataSetBuilder()
        {
            EntitiesFilters = new List<EntityFilter>();
        }
        public virtual void Build(PlatformMap platformMap, Object payload)
        {
            this.Payload = payload;
            BuildFilters(platformMap);
            ShouldBeExecuted();
        }

        private void BuildFilters(PlatformMap platformMap)
        {
            var StringReader = new StringReader(platformMap.Content);
            var deserializer = new DeserializerBuilder().Build();
            var YamlObject = deserializer.Deserialize<Dictionary<string, Dictionary<object, object>>>(StringReader);

            foreach (var key in YamlObject.Keys)
            {
                var EntityFilter = new EntityFilter();
                EntityFilter.EntityName = key;
                EntitiesFilters.Add(EntityFilter);

                var YamlFilter = YamlObject[key]["filters"];
                if (YamlFilter != null)
                {
                    var Filters = ((Dictionary<object, object>)YamlFilter);
                    foreach (var filterKey in Filters.Keys)
                    {
                        EntityFilter.addFilter(new Filter((string)filterKey, (string)Filters[filterKey]));
                    }
                }
            }
        }

        private void ShouldBeExecuted()
        {
            foreach (var EntityFilter in EntitiesFilters)
            {
                foreach (var Filter in EntityFilter.Filters)
                {
                    if ("all".Equals(Filter.Name))
                    {
                        Filter.ShouldBeExecuted = true;
                    }
                    else
                    {
                        var filterParameters = GetFilterParameters(Filter.Query);
                        VerifyEntityAttributes(Filter, filterParameters);
                    }
                }
            }
        }

        private void VerifyEntityAttributes(Filter filter, List<string> filterParameters)
        {
            var EntityProperties = Payload.GetType().GetProperties();
            foreach (var property in EntityProperties)
            {
                var PropertyValue = property.GetValue(Payload);
                if (PropertyValue != null && 
                        filterParameters.FindIndex(filterParameter => filterParameter.Substring(1).Equals(property.Name)) >= 0)
                {
                    filter.ShouldBeExecuted = true;
                    filter.Parameters.Add(property.Name, PropertyValue);
                }
            }
        }

        private List<string> GetFilterParameters(string query)
        {
            var Parameters = new List<string>();
            foreach (Match m in Regex.Matches(query, @"[$|:]\w+"))
            {
                Parameters.Add(m.Value);
            }
            return Parameters;
        }
    }

}