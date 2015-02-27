using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DavidNews.Common
{
    public static class Settings
    {
        [DebuggerStepThrough]
        public static string GetSetting(string key, string defaultValue = "")
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                    return RoleEnvironment.GetConfigurationSettingValue(key);
            }
            catch (RoleEnvironmentException)  // The configuration setting that was being retrieved does not exist.
            {
            }
            return ConfigurationManager.AppSettings.AllKeys.Contains(key)
                ? ConfigurationManager.AppSettings[key]
                : defaultValue;
        }

        [DebuggerStepThrough]
        public static int GetSetting(string key, int defaultValue)
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                    return int.Parse(RoleEnvironment.GetConfigurationSettingValue(key));
            }
            catch (RoleEnvironmentException)  // The configuration setting that was being retrieved does not exist.
            {
            }
            return ConfigurationManager.AppSettings.AllKeys.Contains(key)
                ? int.Parse(ConfigurationManager.AppSettings[key])
                : defaultValue;
        }

        [DebuggerStepThrough]
        public static bool GetSetting(string key, bool defaultValue)
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                    return bool.Parse(RoleEnvironment.GetConfigurationSettingValue(key));
            }
            catch (RoleEnvironmentException)  // The configuration setting that was being retrieved does not exist.
            {
            }
            return ConfigurationManager.AppSettings.AllKeys.Contains(key)
                ? bool.Parse(ConfigurationManager.AppSettings[key])
                : defaultValue;
        }

        [DebuggerStepThrough]
        public static string GetConnectionString(string connectionStringName)
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                    return RoleEnvironment.GetConfigurationSettingValue(connectionStringName);
            }
            catch (RoleEnvironmentException)  // The configuration setting that was being retrieved does not exist.
            {
            }
            return ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }
    }
}
