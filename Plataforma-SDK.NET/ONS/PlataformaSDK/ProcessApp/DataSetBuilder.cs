using System;
using ONS.PlataformaSDK.Entities;
using System.IO;
using YamlDotNet.Serialization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ONS.PlataformaSDK.Domain;
using System.Threading.Tasks;
using System.Reflection;
using ONS.PlataformaSDK.Constants;
using Newtonsoft.Json.Linq;

namespace ONS.PlataformaSDK.ProcessApp
{
    public class DataSetBuilder
    {
        public List<EntityFilter> EntitiesFilters;
        public IDomainContext DomainContext { get; set; }
        private JObject Payload;
        private DomainClient DomainClient;
        public string MapName { get; set; }

        public DataSetBuilder(IDomainContext domainContext, DomainClient domainClient)
        {
            this.DomainContext = domainContext;
            this.DomainClient = domainClient;
            EntitiesFilters = new List<EntityFilter>();
        }
        public virtual void Build(PlatformMap platformMap, dynamic payload)
        {
            this.Payload = payload;
            this.MapName = platformMap.Name;
            BuildFilters(platformMap);
            ShouldBeExecuted();
            LoadDataSet();
        }

        public void Persist()
        {
            var ChangeTrackList = DomainContext.GetPersistList().FindAll(baseEntity => baseEntity._Metadata != null && baseEntity._Metadata.ChangeTrack != null
                && (DomainConstants.CHANGE_TRACK_CREATE.Equals(baseEntity._Metadata.ChangeTrack) ||
                    DomainConstants.CHANGE_TRACK_UPDATE.Equals(baseEntity._Metadata.ChangeTrack) ||
                    DomainConstants.CHANGE_TRACK_DELETE.Equals(baseEntity._Metadata.ChangeTrack)));
            DomainClient.Persist(ChangeTrackList, MapName);
        }

        private void BuildFilters(PlatformMap platformMap)
        {
            if (platformMap.Content != null)
            {
                var StringReader = new StringReader(platformMap.Content);
                var deserializer = new DeserializerBuilder().Build();
                var YamlObject = deserializer.Deserialize<Dictionary<string, Dictionary<object, object>>>(StringReader);
                foreach (var key in YamlObject.Keys)
                {
                    if (YamlObject[key].ContainsKey("filters"))
                    {
                        var EntityFilter = new EntityFilter();
                        EntityFilter.EntityName = key;
                        EntityFilter.MapName = platformMap.Name;
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
            }
        }

        private void ShouldBeExecuted()
        {
            foreach (var EntityFilter in EntitiesFilters)
            {
                if (DomainContextContainsCollection(EntityFilter))
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
        }

        private void LoadDataSet()
        {
            foreach (var EntityFilter in EntitiesFilters)
            {
                foreach (var Filter in EntityFilter.Filters)
                {
                    if (Filter.ShouldBeExecuted)
                    {
                        var Properties = DomainContext.GetType().GetProperties();
                        foreach (var Property in Properties)
                        {
                            if (Property.Name.ToLower().Equals(EntityFilter.EntityName.ToLower()))
                            {
                                var GenericType = Property.PropertyType.GenericTypeArguments[0];
                                var FindByFilterMethod = DomainClient.GetType().GetMethod("FindByFilterAsync");
                                var GenericMethod = FindByFilterMethod.MakeGenericMethod(GenericType);
                                var Result = GenericMethod.Invoke(DomainClient, new object[] { EntityFilter, Filter });
                                // var ResultProperty = ResultTask.GetType().GetProperty("Result");
                                AddToDomainContext(Result, Property);
                            }
                        }
                    }
                }
            }
        }

        private void AddToDomainContext(object result, PropertyInfo property)
        {
            var PropertyType = property.PropertyType;
            var ConcatMethod = PropertyType.GetMethod("AddRange");
            var DataSetList = property.GetValue(DomainContext, null);
            ConcatMethod.Invoke(DataSetList, new object[] { result });
        }

        private bool DomainContextContainsCollection(EntityFilter entityFilter)
        {
            var Properties = DomainContext.GetType().GetProperties();
            foreach (var Property in Properties)
            {
                if (Property.Name.ToLower().Equals(entityFilter.EntityName.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        private void VerifyEntityAttributes(Filter filter, List<string> filterParameters)
        {
            foreach (var property in Payload.Properties())
            {
                var PropertyValue = property.Value;
                if(filterParameters.FindIndex(filterParameter => filterParameter.Substring(1).Equals(property.Name)) >= 0)
                {
                    filter.ShouldBeExecuted = true;
                    filter.Parameters.Add(property.Name, PropertyValue.ToString());
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