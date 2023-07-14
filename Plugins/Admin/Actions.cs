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

namespace ITAintBoring.AdvancedDuplicateDetection.Admin
{

    public class Actions : IPlugin
    {

        public Actions(string unsecureString, string secureString)
        {
        }

        /*
         * stepName - how to name the step
         * ita_sdkmessagename - sdkmessagename
         * ita_attributes - attributes
         * description - step description
         * configuration - step configuration
         * stepId - stepid for existing step (for update/delete)
         * action - CreateStep/UpdateStep/DeleteStep
         * ita_tablename - table/enttiy name
         * 
         */

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            string action = (string)context.InputParameters["ita_action"];
            Guid sdkMessageProcessingStepId = context.InputParameters.Contains("ita_stepid") ? (Guid)context.InputParameters["ita_stepid"] : Guid.Empty;

            if (action == "DeleteStep")
            {
                service.Delete("sdkmessageprocessingstep", sdkMessageProcessingStepId);
            }
            else
            {

                string stepName = context.InputParameters.Contains("ita_stepname") ? (string)context.InputParameters["ita_stepname"] : null;
                string stepAttributes = context.InputParameters.Contains("ita_attributes") ? (string)context.InputParameters["ita_attributes"] : null;
                string sdkMessageName = context.InputParameters.Contains("ita_sdkmessagename") ? (string)context.InputParameters["ita_sdkmessagename"] : "Create";
                string tableName = context.InputParameters.Contains("ita_tablename") ? (string)context.InputParameters["ita_tablename"] : null;
                string description = context.InputParameters.Contains("ita_description") ? (string)context.InputParameters["ita_description"] : null;
                string configuration = context.InputParameters.Contains("ita_configuration") ? (string)context.InputParameters["ita_configuration"] : null;
                OptionSetValue mode = new OptionSetValue(0);
                int rank = 1;
                OptionSetValue stage = new OptionSetValue(10);
                OptionSetValue supportedDeployment = new OptionSetValue(0);
                OptionSetValue invocationSource = new OptionSetValue(0);
                

                //Get plugin type id for DuplicateDetection
                QueryExpression qe = new QueryExpression("plugintype");
                qe.Criteria.AddCondition("name", ConditionOperator.Equal, "ITAintBoring.AdvancedDuplicateDetection.BusinessLogic.DuplicateDetection");
                qe.ColumnSet = new ColumnSet("plugintypeid");
                var pluginTypeResult = service.RetrieveMultiple(qe).Entities.FirstOrDefault();

                EntityReference pluginTypeId = pluginTypeResult != null ? pluginTypeResult.ToEntityReference() : null;


                //Get message id for Create
                qe = new QueryExpression("sdkmessage");
                qe.Criteria.AddCondition("name", ConditionOperator.Equal, sdkMessageName);
                qe.ColumnSet = new ColumnSet("sdkmessageid");
                var sdkMessageResult = service.RetrieveMultiple(qe).Entities.FirstOrDefault();



                EntityReference sdkMessageId = sdkMessageResult != null ? sdkMessageResult.ToEntityReference() : null;


                EntityReference sdkMessageFilterId = null;

                if (sdkMessageId != null && action != "UpdateStep")
                {
                    //Get message id for Create
                    qe = new QueryExpression("sdkmessagefilter");
                    qe.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, tableName);
                    qe.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, sdkMessageId.Id);
                    qe.ColumnSet = new ColumnSet("sdkmessagefilterid");
                    var sdkMessageFilterResult = service.RetrieveMultiple(qe).Entities.FirstOrDefault();
                    sdkMessageFilterId = sdkMessageFilterResult != null ? sdkMessageFilterResult.ToEntityReference() : null;
                }


                //throw new InvalidPluginExecutionException((string)sdkMessageFilterResult["primaryobjecttypecode"]);

                if (action == "CreateStep")
                {
                    Entity step = new Entity("sdkmessageprocessingstep");
                    step["name"] = stepName;
                    step["description"] = description;
                    step["configuration"] = configuration;
                    step["mode"] = mode;
                    step["rank"] = rank;
                    step["stage"] = stage;
                    step["supporteddeployment"] = supportedDeployment;
                    step["invocationsource"] = invocationSource;
                    step["plugintypeid"] = pluginTypeId;
                    step["sdkmessageid"] = sdkMessageId;
                    step["filteringattributes"] = stepAttributes;
                    step["sdkmessagefilterid"] = sdkMessageFilterId;
                    step.Id = service.Create(step);
                }
                else if (action == "UpdateStep")
                {
                    Entity step = new Entity("sdkmessageprocessingstep");
                    //step["name"] = stepName;
                    step["description"] = description;
                    step["configuration"] = configuration;
                    step["filteringattributes"] = stepAttributes;
                    //step["mode"] = mode;
                    //step["rank"] = rank;
                    //step["stage"] = stage;
                    //step["supporteddeployment"] = supportedDeployment;
                    //step["invocationsource"] = invocationSource;
                    //step["plugintypeid"] = pluginTypeId;
                    //step["sdkmessageid"] = sdkMessageId;
                    //step["sdkmessagefilterid"] = sdkMessageFilterId;
                    step.Id = sdkMessageProcessingStepId;
                    service.Update(step);
                }
            }
  

        }
    }
}