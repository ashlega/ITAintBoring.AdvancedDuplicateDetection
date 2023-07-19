using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;

namespace ITAintBoring.AdvancedDuplicateDetection.BusinessLogic
{
    [DataContract]
    public class QueryData
    {
        [DataMember] public string FetchXML;
        [DataMember] public string ErrorMessage;
        [DataMember] public bool ErrorWhenEmpty = false;
        
        [DataMember] public bool ErrorWhenNotEmpty = false;
        [DataMember] public bool SuccessWhenEmpty = false;
        [DataMember] public bool SuccessWhenNotEmpty = false;
        [DataMember] public string QueryName;
        [DataMember] public int QueryOrder;
        

        public Entity result = null;

    }

    [DataContract]
    public class PluginConfig
    {
        [DataMember] public QueryData[] QueryList;
        [DataMember] public bool HideQueryErrors = false;

        [DataMember] public string FetchXML;
        [DataMember] public string ErrorMessage;

        [DataMember] public bool ErrorWhenEmpty;
        [DataMember] public bool IgnoreQueryErrors = false;

    }

    public class DuplicateDetection : IPlugin
    {


        PluginConfig config = null;


        public DuplicateDetection(string unsecureString, string secureString)
        {
            if (unsecureString == null) return;
            
            string configJson = unsecureString.Replace('\r', ' ').Replace('\n', ' ');
            var deserializedConfig = new PluginConfig();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(configJson));
            var ser = new DataContractJsonSerializer(deserializedConfig.GetType());
            config = ser.ReadObject(ms) as PluginConfig;
            ms.Close();
            List<QueryData> queryList = null;
            if (config.QueryList == null || config.QueryList.Length == 0)
            {
                queryList = new List<QueryData>();
                queryList.Add(new QueryData()
                {
                    ErrorMessage = config.ErrorMessage,
                    ErrorWhenEmpty = config.ErrorWhenEmpty,
                    FetchXML = config.FetchXML,
                    QueryName = "Main",
                    QueryOrder = 1
                }
                );
                config.QueryList = queryList.ToArray();
            }
            else
            {
                queryList = new List<QueryData>(config.QueryList);
            }
            queryList.Sort((a, b) => { return a.QueryOrder - b.QueryOrder; });
            config.QueryList = queryList.ToArray();
        }


        public static string getStringValue(Entity record, string key)

        {

            string result = "";
            object value = null;
            
            if (!record.Contains(key)) return result;

            if (record[key] is AliasedValue)
            {
                value = ((AliasedValue)record[key]).Value;
            }
            else
            {
                value = record[key];
            }

            if (value is int) result = (value).ToString();
            else if (value is decimal) result = (value).ToString();
            else if (value is Money) result = ((Money)value).Value.ToString();
            else if (value is EntityReference) result += ((EntityReference)value).Id.ToString();
            else if (value is OptionSetValue) result = ((OptionSetValue)record[key]).Value.ToString();
            else if (value is bool) result = ((bool)record[key]).ToString();
            else if (value is DateTime) result = ((DateTime)record[key]).ToString();
            else if (value is string) result += (string)value;
            else if (value is Guid) result += value.ToString();

            return result;
        }

        public string ProcessTemplate(Entity record, string templatePrefix, string fetchXml)
        {
            if (record != null)
            {
                string value = null;
                foreach (var attr in record.Attributes)
                {
                    value = getStringValue(record, attr.Key);
                    fetchXml = fetchXml.Replace("{" + (templatePrefix != null ? templatePrefix + "." : "") + attr.Key + "}", value);
                }
                fetchXml = fetchXml.Replace("{" + (templatePrefix != null ? templatePrefix + "." : "") + record.LogicalName + "id}", record.Id.ToString());
            }
            return fetchXml;
        }
        public void Execute(IServiceProvider serviceProvider)
        {
            
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            Entity target = null;
            if(context.InputParameters["Target"] is EntityReference)
            {
                EntityReference targetRef = (EntityReference)context.InputParameters["Target"];
                target = new Entity(targetRef.LogicalName, targetRef.Id);
            }
            else target = (Entity)context.InputParameters["Target"];

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            if (config != null)
            {
                for(int queryIndex = 0; queryIndex < config.QueryList.Length; queryIndex++)
                {
                    var query = config.QueryList[queryIndex];
                    string fetchXml = query.FetchXML;
                    bool goodUpToTheQuery = false;
                    try
                    {

                        string value = context.InitiatingUserId.ToString();
                        fetchXml = fetchXml.Replace("{modifiedby}", value);
                        //fetchXml = fetchXml.Replace("{id}", context.PrimaryEntityId.ToString());
                        fetchXml = ProcessTemplate(target, null, fetchXml);

                        for(int i = 0; i < queryIndex; i++)
                        {
                            
                            fetchXml = ProcessTemplate(config.QueryList[i].result, config.QueryList[i].QueryName, fetchXml);
                        }
                                                
                        FetchExpression fe = new FetchExpression(fetchXml);
                        goodUpToTheQuery = true;
                        query.result = service.RetrieveMultiple(fe).Entities.FirstOrDefault();

                    }
                    catch (Exception ex)
                    {
                        if (!goodUpToTheQuery || !config.HideQueryErrors)
                        {
                            throw new InvalidPluginExecutionException("Query: " + query.QueryName + ". " + ex.Message + fetchXml);
                        }
                        else return;
                        
                    }
                    //throw new InvalidPluginExecutionException(count.ToString() + config.ErrorWhenEmpty.ToString());

                    if(query.result != null && query.ErrorWhenNotEmpty ||
                       query.result == null && query.ErrorWhenEmpty)
                    {
                        throw new InvalidPluginExecutionException(query.ErrorMessage);
                    }
                    if (query.result != null && query.SuccessWhenNotEmpty ||
                       query.result == null && query.SuccessWhenEmpty)
                    {
                        return;
                    }

                    
                }

            }
            
        }
    }
}