using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using MediaBrowser.Library.Logging;

namespace Configurator.Code
{
    public class EntryPointManager
    {
        private EntryPointItem _MainMBEntryPoint = null;

        public EntryPointManager()
        {
            if (!TestRegistryAccess())
            {
                throw new Exception("This account does not have suffisant privileges to write to the registry.");
            }

            this._MainMBEntryPoint = FetchEntryPoint(Constants.MB_MAIN_ENTRYPOINT_GUID);

            try
            {
                if (ValidateHiddenEntryPoint(Constants.MB_CONFIG_ENTRYPOINT_GUID) == false)
                {
                    CreateHiddenCategoryEntryPoint(Constants.MB_CONFIG_ENTRYPOINT_GUID);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error validating Hidden entrypoint for " + Constants.MB_CONFIG_ENTRYPOINT_GUID + ". " + ex.Message);
            }
        }

        public void ValidateEntryPoints(List<EntryPointItem> entryPoints)
        {
            try
            {               
                List<EntryPointItem> EntryPointsInRegistry = FetchMediaBrowserEntryPoints();

                
                // Add any missing entry Points
                foreach (var ep in entryPoints)
                {                    
                    bool epMatchFound = false;
                    
                    foreach (var RegEp in EntryPointsInRegistry)
                    {
                        if (ep.Context.Value.ToString().ToLower().Trim() == RegEp.Context.Value.ToString().ToLower().Trim())
                        {                            
                            if (ep.Title.Value.ToString() != RegEp.Title.Value.ToString())
                            {
                                this.RenameEntryPointTitle(RegEp.Title.Value.ToString(), ep.Title.Value.ToString(), ep.Context.Value.ToString(), ep.Context.Value.ToString());
                            }
                            
                            if (!ValidateHiddenEntryPoint(RegEp.GUID))
                            {
                                CreateHiddenCategoryEntryPoint(RegEp.GUID);
                            }
                            
                            epMatchFound = true;
                            break;
                        }
                    }
                    
                    if (!epMatchFound)
                    {                       
                        CreateEntryPoint(ep);
                    }
                }
                // Delete any left over entry points
                foreach (var RegEp in EntryPointsInRegistry)
                { 
                    bool epMatchFound = false;

                    foreach (var ep in entryPoints)
                    {
                        if (ep.Context.Value.ToString().ToLower().Trim() == RegEp.Context.Value.ToString().ToLower().Trim())
                        {
                            epMatchFound = true;
                            break;
                        }
                    }
                    
                    if (!epMatchFound)
                    {
                        DeleteEntryPointKey(RegEp.GUID);                        
                    } 
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error validating entrypoints. " + ex.Message, ex);
                throw new Exception("Error validating entrypoints. " + ex.Message);
            }
        }

        private void CreateHiddenCategoryEntryPoint(String GUID)
        {
            try
            {
                RegistryKey Category = Registry.LocalMachine.OpenSubKey(Constants.CATEGORIES_REGISTRY_PATH, true);

                if (Category.OpenSubKey(Constants.HIDDEN_CATEGORIES_GUID) == null)
                {
                    Category.CreateSubKey(Constants.HIDDEN_CATEGORIES_GUID);                    
                }

                RegistryKey Hidden_CatKey = Category.OpenSubKey(Constants.HIDDEN_CATEGORIES_GUID, true);

                Hidden_CatKey.CreateSubKey(GUID);

                RegistryKey new_EntryPoint_key = Hidden_CatKey.OpenSubKey(GUID, true);

                this.WriteValue(new_EntryPoint_key, new RegistryItem("AppID", RegistryValueKind.String, Constants.APPLICATION_ID));
                                
            }
            catch (Exception ex)
            {
                throw new Exception("Error writing key " + GUID + " to " + Constants.CATEGORIES_REGISTRY_PATH + "\\" + Constants.HIDDEN_CATEGORIES_GUID + ". " + ex.Message);
            }
        }

        private bool ValidateHiddenEntryPoint(String GUID)
        {
            try
            {
                if (Registry.LocalMachine.OpenSubKey(Constants.CATEGORIES_REGISTRY_PATH + "\\" + Constants.HIDDEN_CATEGORIES_GUID + "\\" + GUID) == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.ReportException("Missing key", ex);
                return false;
            }
        }

        private void CreateEntryPoint(EntryPointItem entryPoint)
        {
            try
            {
                String GUID = "{" + CreateGuid().ToString() + "}";
                entryPoint = new EntryPointItem(this._MainMBEntryPoint.AppID.Value, this._MainMBEntryPoint.AddIn.Value, entryPoint.Context.Value, entryPoint.Title.Value, GUID,
                    this._MainMBEntryPoint.Description.Value, this._MainMBEntryPoint.ImageUrl.Value, this._MainMBEntryPoint.InactiveImageUrl.Value);


                this.SaveEntryPoint(entryPoint);

                CreateHiddenCategoryEntryPoint(GUID);                              
            }
            catch (Exception ex)
            {
                throw new Exception("Error creating Entrypoint. " +ex.Message);
            }
        }

        private void SaveEntryPoint(EntryPointItem entryPoint)        
        {
            RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH, true);

            try
            {
                RegistryKey EntryPointsSubKey = EntryPointsTree.OpenSubKey(entryPoint.GUID, true);

                if (EntryPointsSubKey == null)
                {
                    EntryPointsTree.CreateSubKey(entryPoint.GUID);
                    EntryPointsSubKey = EntryPointsTree.OpenSubKey(entryPoint.GUID, true);
                }                

                if (EntryPointsSubKey != null)
                {
                    WriteValue(EntryPointsSubKey, entryPoint.Title);
                    WriteValue(EntryPointsSubKey, entryPoint.Context);
                    WriteValue(EntryPointsSubKey, entryPoint.TimeStamp);
                    WriteValue(EntryPointsSubKey, entryPoint.AddIn);
                    WriteValue(EntryPointsSubKey, entryPoint.AppID);
                    WriteValue(EntryPointsSubKey, entryPoint.Description);
                    WriteValue(EntryPointsSubKey, entryPoint.ImageUrl);
                    WriteValue(EntryPointsSubKey, entryPoint.InactiveImageUrl);
                }
            }
            catch (Exception ex)
            {
                throw new Exception ("Error saving entrypoint. " + ex.Message);
            }
        }

        private void DeleteEntryPointKey(String GUID)
        {
            try
            {
                RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH, true);
                RegistryKey EntryPointsSubKey = EntryPointsTree.OpenSubKey(GUID, true);
                               
                if (EntryPointsSubKey != null && this.FetchAppID(GUID).ToLower() == Constants.APPLICATION_ID.ToLower())
                {
                    EntryPointsTree.DeleteSubKey(GUID);
                }

                DeleteEntryPointKeyFromCategory(GUID); 
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error deleting key", ex);
                throw new Exception("Error deleting key " + GUID);
            }                       
        }

        public void DeleteEntryPointKeyFromCategory(String GUID)
        {
            try
            {
                RegistryKey EntryPointsKey = Registry.LocalMachine.OpenSubKey(Constants.CATEGORIES_REGISTRY_PATH);

                DeleteEntryPointKeyFromCategory_Recursive(GUID, EntryPointsKey);
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error deleting key in DeleteEntryPointKeyFromCategory()", ex);
                throw new Exception("Error deleting key in DeleteEntryPointKeyFromCategory() " + ex.Message);
            }
        }

        private bool DeleteEntryPointKeyFromCategory_Recursive(String GUID, RegistryKey key)
        {
            try
            {
                String[] CatKeySplit = key.Name.Split('\\');

                if (CatKeySplit[CatKeySplit.Length - 1].ToLower() == GUID.ToLower())
                {
                    //delete key                
                    return true;
                }

                foreach (var subkeyStr in key.GetSubKeyNames())
                {
                    RegistryKey CategorySubKey;

                    try
                    {
                        CategorySubKey = key.OpenSubKey(subkeyStr, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.ReportException("Could not open registry key " + subkeyStr + " for write access in DeleteEntryPointKeyFromCategory_Recursive. ", ex);
                        continue;//Move to next key
                    }

                    if (DeleteEntryPointKeyFromCategory_Recursive(GUID, CategorySubKey) == true)
                    {
                        key.DeleteSubKey(subkeyStr);
                    }                    
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error deleting key in DeleteEntryPointKeyFromCategory_Recursive(). " + ex.Message , ex);
                throw new Exception("Error deleting key in DeleteEntryPointKeyFromCategory_Recursive() " + ex.Message + GUID);
            }            
        }

        public void RenameEntryPointTitle(String OldName, String NewName, String OldContextStr, String NewContextStr)
        {
            List<EntryPointItem> entryPoints = this.FetchMediaBrowserEntryPoints();

            foreach (var ep in entryPoints)
            {
                if (OldContextStr.Trim().ToLower() == ep.Context.Value.ToString().ToLower())
                {
                    ep.SetTitle(NewName);
                    ep.SetContext(NewContextStr);
                    this.SaveEntryPoint(ep);
                    break;
                }
            }
        }

        private List<EntryPointItem> FetchMediaBrowserEntryPoints()
        {
            List<EntryPointItem> EntryPoints = new List<EntryPointItem>();

            RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH);

            foreach (var Key in EntryPointsTree.GetSubKeyNames())
            {
                try
                {
                    if (Key.ToLower() == Constants.MB_MAIN_ENTRYPOINT_GUID.ToLower() || Key.ToLower() == Constants.MB_CONFIG_ENTRYPOINT_GUID.ToLower())
                    {
                        continue;
                    }

                    if (FetchAppID(Key).ToLower() == Constants.APPLICATION_ID.ToLower())
                    {
                        EntryPointItem entryPoint = FetchEntryPoint(Key);

                        if (entryPoint != null)
                        {
                            if (FetchAddIn(Key).ToLower() != FetchAddIn(Constants.MB_MAIN_ENTRYPOINT_GUID).ToLower())
                            {// If the value in Addin is differnet than in the main entrypoint key, update the value
                                entryPoint.AddIn.Value = (FetchAddIn(Constants.MB_MAIN_ENTRYPOINT_GUID));
                                SaveEntryPoint(entryPoint);
                            }

                            EntryPoints.Add(entryPoint);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Failed in FetchMediaBrowserEntryPoints " + ex.Message + ". Deleting entrypoing " + Key, ex);
                    this.DeleteEntryPointKey(Key);
                }
            }

            return EntryPoints;
        }

        private EntryPointItem FetchEntryPoint(String EntryPointUID)
        {            
            RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH);

            String AddIn = String.Empty;
            String AppID = String.Empty;
            String Title = String.Empty;
            String Context = String.Empty;
            String Description = String.Empty;
            String ImageURL = String.Empty;
            String InActiveImageUrl = String.Empty;
           
            try
            {
                RegistryKey EntryPointsSubKey = EntryPointsTree.OpenSubKey(EntryPointUID);
                if (EntryPointsSubKey != null)
                {
                    AppID = ReadValue(EntryPointsSubKey, EntryPointItem.AppIdName).Value.ToString();

                    AddIn = ReadValue(EntryPointsSubKey, EntryPointItem.AddInName).Value.ToString();

                    Title = ReadValue(EntryPointsSubKey, EntryPointItem.TitleName).Value.ToString();

                    if (EntryPointUID.ToLower() != Constants.MB_MAIN_ENTRYPOINT_GUID)
                    {
                        try
                        {
                            Context = ReadValue(EntryPointsSubKey, EntryPointItem.ContextName).Value.ToString();
                        }
                        catch (Exception ex)
                        {
                            Logger.ReportException("Failed to read EntryPointItem.ContextName for Title:" + Title + " and GUID:" + EntryPointUID, ex);
                            throw ex;
                        }
                    }                    

                    Description = ReadValue(EntryPointsSubKey, EntryPointItem.DescriptionName).Value.ToString();

                    ImageURL = ReadValue(EntryPointsSubKey, EntryPointItem.ImageUrlName).Value.ToString();

                    InActiveImageUrl = ReadValue(EntryPointsSubKey, EntryPointItem.InactiveImageUrlName).Value.ToString();
                }
                else
                {
                    throw new Exception("Entrypoint " + EntryPointUID + " not found.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching entry point for " + EntryPointUID + ". " + ex.Message);
            }

            return new EntryPointItem(AppID, AddIn, Context, Title, EntryPointUID, Description, ImageURL, InActiveImageUrl);
        }       

        private String FetchAppID(String EntrypointGUID)
        {
            String AppID = String.Empty;

            try
            {
                RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH);
                RegistryKey EntryPointsSubKey = EntryPointsTree.OpenSubKey(EntrypointGUID);
                AppID = ReadValue(EntryPointsSubKey, EntryPointItem.AppIdName).Value.ToString();
            }
            catch (Exception ex)
            {
                Logger.ReportException("Failed to fetch app id", ex);
                AppID = String.Empty;
            }
            return AppID;
        }

        private String FetchAddIn(String EntrypointGUID)
        { 
            try
            {                
                RegistryKey EntryPointsTree = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH);
                RegistryKey EntryPointsSubKey = EntryPointsTree.OpenSubKey(EntrypointGUID);
                String AddIn = ReadValue(EntryPointsSubKey, EntryPointItem.AddInName).Value.ToString();
                return AddIn;
            }
            catch (Exception ex)
            {
                throw new Exception("Error reading AddIn Registry Key. " + ex.Message);
            }            
        }

        private RegistryItem ReadValue(RegistryKey regKeyPath, String ValueKey)
        {
            try
            {
                if (regKeyPath.ValueCount == 0)
                {
                    throw new Exception("There are no value keys in the path " + regKeyPath.Name);
                }
                String Value = regKeyPath.GetValue(ValueKey).ToString();

                if (Value == null)
                {
                    throw new Exception("Reg key " + ValueKey + " does not exist in " + regKeyPath.Name);
                }

                RegistryValueKind KeyType = regKeyPath.GetValueKind(ValueKey);

                return new RegistryItem(ValueKey, KeyType, Value);
            }
            catch (Exception ex)
            {
                throw new Exception("Error recieving registry key. " + ex.Message);
            }
        }

        private void WriteValue(RegistryKey regKeyPath, RegistryItem regItem)
        {
            try
            {
                if (regKeyPath == null)
                {
                    throw new Exception("RegKeyPath is null for item " + regItem.Name + ".");
                }
                if (regItem.Name == String.Empty)
                {
                    throw new Exception("One or more of the values in " + regItem.Name + " are incomplete.");
                }

                try
                {
                    regKeyPath.SetValue(regItem.Name, regItem.Value, regItem.type);
                }
                catch (Exception ex)
                {
                    throw new Exception("Error writing " + regItem.Value + " to " + regItem.Name + " of type " + regItem.type.ToString() + " in the path of " + regKeyPath.Name + ". " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error writing registry key. " + ex.Message);
            }
        }

        private Guid CreateGuid()
        {
            Guid guid = System.Guid.NewGuid();
            return guid;
        }

        private bool TestRegistryAccess()
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(Constants.ENTRYPOINTS_REGISTRY_PATH + @"\" + Constants.MB_MAIN_ENTRYPOINT_GUID, true);

                RegistryItem items = ReadValue(key, EntryPointItem.TimeStampName);
                WriteValue(key, items);
            }
            catch (Exception ex)
            {
                Logger.ReportException("Failed when testing for regitry access", ex);
                return false;
            }
            return true;
        }
    }

    #region Class EntryPointItem
    public class EntryPointItem
    {
        public static readonly String AppIdName = "AppID";
        public static readonly String AddInName = "AddIn";
        public static readonly String ContextName = "Context";
        public static readonly String TitleName = "Title";
        public static readonly String TimeStampName = "TimeStamp";
        public static readonly String DescriptionName = "Description";        
        public static readonly String ImageUrlName = "ImageUrl";
        public static readonly String InactiveImageUrlName = "InactiveImageUrl";

        private RegistryItem _AppID = new RegistryItem(AppIdName, RegistryValueKind.String, Constants.APPLICATION_ID);
        private RegistryItem _AddIn = new RegistryItem(AddInName, RegistryValueKind.ExpandString, String.Empty);        
        private RegistryItem _Context = new RegistryItem(ContextName, RegistryValueKind.ExpandString, String.Empty);
        private RegistryItem _Title = new RegistryItem(TitleName, RegistryValueKind.ExpandString, String.Empty);
        private RegistryItem _TimeStamp = new RegistryItem(TimeStampName, RegistryValueKind.DWord, (new Random()).Next(100, 100000).ToString());
        private RegistryItem _Description = new RegistryItem(DescriptionName, RegistryValueKind.ExpandString, String.Empty);
        private RegistryItem _ImageUrl = new RegistryItem(ImageUrlName, RegistryValueKind.ExpandString, String.Empty);
        private RegistryItem _InactiveImageUrl = new RegistryItem(InactiveImageUrlName, RegistryValueKind.ExpandString, String.Empty);
       
        private String _GUID = String.Empty;

        public RegistryItem AppID
        {
            get
            {
                return this._AppID;
            }
        }
        public RegistryItem AddIn
        {
            get
            {
                return this._AddIn;
            }
        }        
        public RegistryItem Context
        {
            get
            {
                return this._Context;
            }
        }       
        public RegistryItem Title
        {
            get
            {
                return this._Title;
            }
        }
        public RegistryItem TimeStamp
        {
            get
            {
                return this._TimeStamp;
            }
        }
        public RegistryItem Description
        {
            get
            {
                return this._Description;
            }
        }
        public RegistryItem ImageUrl
        {
            get
            {
                return this._ImageUrl;
            }
        }
        public RegistryItem InactiveImageUrl
        {
            get
            {
                return this._InactiveImageUrl;
            }
        }
        public String GUID
        {
            get
            {
                return _GUID;
            }
        }

        public void SetTitle(String title)
        {
            this._Title = new RegistryItem(TitleName, RegistryValueKind.ExpandString, title);
        }
        public void SetContext(String Context)
        {
            _Context = new RegistryItem(ContextName, RegistryValueKind.ExpandString, Context);
        }

        public EntryPointItem(String AppId, String AddIn, String Context, String DisplayName, String GUID, String Description, String ImageUrl, String InactiveImageUrl )
        {
            _AppID = new RegistryItem(AppIdName, RegistryValueKind.String, AppId);
            _AddIn = new RegistryItem(AddInName, RegistryValueKind.ExpandString, AddIn); 
            _Title = new RegistryItem(TitleName, RegistryValueKind.ExpandString, DisplayName);
            _Context = new RegistryItem(ContextName, RegistryValueKind.ExpandString, Context);
            _GUID = GUID;
            _Description = new RegistryItem(DescriptionName, RegistryValueKind.ExpandString, Description);
            _ImageUrl = new RegistryItem(ImageUrlName, RegistryValueKind.ExpandString, ImageUrl);
            _InactiveImageUrl = new RegistryItem(InactiveImageUrlName, RegistryValueKind.ExpandString, InactiveImageUrl);
        }

        public EntryPointItem(String DisplayName, String Context)
        {
            _Title = new RegistryItem(TitleName, RegistryValueKind.ExpandString, DisplayName);
            _Context = new RegistryItem(ContextName, RegistryValueKind.ExpandString, Context);            
        }

        public override string ToString()
        {
            return this.Title.Name;
        }
    }
    #endregion

    #region Class RegistryItem
    public class RegistryItem
    {
        private String _Name = String.Empty;
        private RegistryValueKind _type;
        private String _Value = String.Empty;

        public String Name { get { return this._Name; } }
        public RegistryValueKind type { get { return this._type; } }
        public String Value { set { this._Value = value; } get { return this._Value; } }

        public RegistryItem(String Name, RegistryValueKind type, String Value)
        {
            this._Name = Name;
            this._type = type;
            this._Value = Value;

            if (this._Value.ToLower() == "null".ToLower())
            {
                this._Value = String.Empty;
            }
        }
    }
    #endregion
}
