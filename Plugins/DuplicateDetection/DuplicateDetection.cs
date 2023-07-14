using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace ITAintBoring.AdvancedDuplicateDetection.BusinessLogic
{
    [DataContract]
    public class PluginConfig
    {
        [DataMember] public string FetchXML;
        [DataMember] public string ErrorMessage;
        [DataMember] public bool ErrorWhenEmpty;

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
            
            return result;
        }


        public void Execute(IServiceProvider serviceProvider)
        {
            
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var target = (Entity)context.InputParameters["Target"];

            var service = serviceFactory.CreateOrganizationService(context.UserId);

            if (config != null)
            {
                string fetchXml = config.FetchXML;
                int count = 0;
                try
                {
                   
                    string value = null;
                    foreach (var attr in target.Attributes)
                    {
                        value = getStringValue(target, attr.Key);
                        fetchXml = fetchXml.Replace("{" + attr.Key + "}", value);
                    }

                    value = context.InitiatingUserId.ToString();
                    fetchXml = fetchXml.Replace("{modifiedby}", value);
                    FetchExpression fe = new FetchExpression(fetchXml);
                    count = service.RetrieveMultiple(fe).Entities.Count;
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message + fetchXml);
                }

                //throw new InvalidPluginExecutionException(count.ToString() + config.ErrorWhenEmpty.ToString());
                if ((count > 0 && !config.ErrorWhenEmpty) ||
                    (count == 0 && config.ErrorWhenEmpty))
                {
                    throw new InvalidPluginExecutionException(config.ErrorMessage);
                }
                

            }
            
        }
    }
}