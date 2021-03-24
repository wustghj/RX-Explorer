﻿using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Size = System.Drawing.Size;
using Timer = System.Threading.Timer;

namespace FullTrustProcess
{
    class Program
    {
        private static AppServiceConnection Connection;

        private static readonly Dictionary<string, NamedPipeServerStream> PipeServers = new Dictionary<string, NamedPipeServerStream>();

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        private static readonly object Locker = new object();

        private static Timer AliveCheckTimer;

        private static Process ExplorerProcess;

        static async Task Main(string[] args)
        {
            try
            {
                Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                };
                Connection.RequestReceived += Connection_RequestReceived;
                Connection.ServiceClosed += Connection_ServiceClosed;

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    AliveCheckTimer = new Timer(AliveCheck, null, 10000, 10000);

                    //Loading the menu in advance can speed up the re-generation speed and ensure the stability of the number of menu items
                    await ContextMenu.FetchContextMenuItemsAsync(Environment.GetEnvironmentVariable("TMP"), true);
                }
                else
                {
                    ExitLocker.Set();
                }

                ExitLocker.WaitOne();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"FullTrustProcess出现异常，错误信息{e.Message}");
            }
            finally
            {
                Connection?.Dispose();
                ExitLocker?.Dispose();
                AliveCheckTimer?.Dispose();

                try
                {
                    PipeServers.Values.ToList().ForEach((Item) =>
                    {
                        Item.Dispose();
                    });
                }
                catch
                {
                    Debug.WriteLine("Error when dispose PipeLine");
                }

                PipeServers.Clear();

                Environment.Exit(0);
            }
        }

        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            ExitLocker.Set();
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExecuteType"])
                {
                    case "Execute_GetDocumentProperties":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                Dictionary<string, string> PropertiesDic = new Dictionary<string, string>(9);

                                using (ShellItem Item = new ShellItem(ExecutePath))
                                {
                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.LastAuthor, out string LastAuthor))
                                    {
                                        PropertiesDic.Add("LastAuthor", LastAuthor);
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("LastAuthor", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.Version, out string Version))
                                    {
                                        PropertiesDic.Add("Version", Version);
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("Version", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.RevisionNumber, out string RevisionNumber))
                                    {
                                        PropertiesDic.Add("RevisionNumber", RevisionNumber);
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("RevisionNumber", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.PageCount, out int PageCount))
                                    {
                                        PropertiesDic.Add("PageCount", Convert.ToString(PageCount));
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("PageCount", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.WordCount, out int WordCount))
                                    {
                                        PropertiesDic.Add("WordCount", Convert.ToString(WordCount));
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("WordCount", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.CharacterCount, out int CharacterCount))
                                    {
                                        PropertiesDic.Add("CharacterCount", Convert.ToString(CharacterCount));
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("CharacterCount", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.LineCount, out int LineCount))
                                    {
                                        PropertiesDic.Add("LineCount", Convert.ToString(LineCount));
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("LineCount", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.Template, out string Template))
                                    {
                                        PropertiesDic.Add("Template", Template);
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("Template", string.Empty);
                                    }

                                    if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Document.TotalEditingTime, out ulong TotalEditingTime))
                                    {
                                        PropertiesDic.Add("TotalEditingTime", Convert.ToString(TotalEditingTime));
                                    }
                                    else
                                    {
                                        PropertiesDic.Add("TotalEditingTime", string.Empty);
                                    }
                                }

                                Value.Add("Success", JsonSerializer.Serialize(PropertiesDic));
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetMIMEContentType":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet
                            {
                                { "Success", MIMEHelper.GetMIMEFromPath(ExecutePath)}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetHiddenItemInfo":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            using (ShellItem Item = ShellItem.Open(ExecutePath))
                            using (Image Thumbnail = Item.GetImage(new Size(128, 128), ShellItemGetImageOptions.BiggerSizeOk))
                            using (Bitmap OriginBitmap = new Bitmap(Thumbnail))
                            using (MemoryStream Stream = new MemoryStream())
                            {
                                OriginBitmap.MakeTransparent();
                                OriginBitmap.Save(Stream, ImageFormat.Png);

                                ValueSet Value = new ValueSet
                                {
                                    {"Success", JsonSerializer.Serialize(new HiddenDataPackage(Item.FileInfo.TypeName, Stream.ToArray()))}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }

                            break;
                        }
                    case "Execute_CheckIfEverythingAvailable":
                        {
                            await args.Request.SendResponseAsync(new ValueSet
                            {
                                {"Success", EverythingConnector.Current.IsAvailable }
                            });

                            break;
                        }
                    case "Execute_SearchByEverything":
                        {
                            string BaseLocation = Convert.ToString(args.Request.Message["BaseLocation"]);
                            string SearchWord = Convert.ToString(args.Request.Message["SearchWord"]);
                            bool SearchAsRegex = Convert.ToBoolean(args.Request.Message["SearchAsRegex"]);
                            bool IgnoreCase = Convert.ToBoolean(args.Request.Message["IgnoreCase"]);
                            uint MaxCount = Convert.ToUInt32(args.Request.Message["MaxCount"]);

                            ValueSet Value = new ValueSet();

                            if (EverythingConnector.Current.IsAvailable)
                            {
                                IEnumerable<string> SearchResult = EverythingConnector.Current.Search(BaseLocation, SearchWord, SearchAsRegex, IgnoreCase, MaxCount);

                                if (SearchResult.Any())
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                }
                                else
                                {
                                    EverythingConnector.StateCode Code = EverythingConnector.Current.GetLastErrorCode();

                                    if (Code == EverythingConnector.StateCode.OK)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                    }
                                    else
                                    {
                                        Value.Add("Error", $"Everything report an error, code: {Enum.GetName(typeof(EverythingConnector.StateCode), Code)}");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Everything is not available");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetContextMenuItems":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            bool IncludeExtensionItem = Convert.ToBoolean(args.Request.Message["IncludeExtensionItem"]);

                            ContextMenuPackage[] ContextMenuItems = await ContextMenu.FetchContextMenuItemsAsync(ExecutePath, IncludeExtensionItem);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", JsonSerializer.Serialize(ContextMenuItems) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_InvokeContextMenuItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string Verb = Convert.ToString(args.Request.Message["InvokeVerb"]);
                            int Id = Convert.ToInt32(args.Request.Message["InvokeId"]);

                            ValueSet Value = new ValueSet();

                            if (!string.IsNullOrWhiteSpace(Path))
                            {
                                if (await ContextMenu.InvokeVerbAsync(Path, Verb, Id))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", $"Execute Id: \"{Id}\", Verb: \"{Verb}\" failed");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path could not be empty");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_CreateLink":
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            string Argument = string.Join(" ", Package.Argument.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para).ToArray());

                            using (ShellLink Link = ShellLink.Create(StorageItemController.GenerateUniquePath(Package.LinkPath), Package.LinkTargetPath, Package.Comment, Package.WorkDirectory, Argument))
                            {
                                Link.ShowState = (FormWindowState)Package.WindowState;
                                Link.RunAsAdministrator = Package.NeedRunAsAdmin;

                                if (Package.HotKey > 0)
                                {
                                    Link.HotKey = (((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                }
                            }

                            ValueSet Value = new ValueSet
                            {
                                { "Success", string.Empty }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetVariable_Path":
                        {
                            ValueSet Value = new ValueSet();

                            string Variable = Convert.ToString(args.Request.Message["Variable"]);

                            string Env = Environment.GetEnvironmentVariable(Variable);

                            if (string.IsNullOrEmpty(Env))
                            {
                                Value.Add("Error", "Could not found EnvironmentVariable");
                            }
                            else
                            {
                                Value.Add("Success", Env);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Rename":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string DesireName = Convert.ToString(args.Request.Message["DesireName"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                            {
                                if (StorageItemController.CheckOccupied(ExecutePath))
                                {
                                    Value.Add("Error_Occupied", "FileLoadException");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(FileSystemRights.Modify, Path.GetDirectoryName(ExecutePath)))
                                    {
                                        if (!StorageItemController.Rename(ExecutePath, DesireName, (s, e) =>
                                        {
                                            Value.Add("Success", e.Name);
                                        }))
                                        {
                                            Value.Add("Error_Failure", "Error happened when rename");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "No Modify Permission");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "FileNotFoundException");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetInstalledApplication":
                        {
                            string PFN = Convert.ToString(args.Request.Message["PackageFamilyName"]);

                            InstalledApplicationPackage Pack = await Helper.GetInstalledApplicationAsync(PFN).ConfigureAwait(true);

                            if (Pack != null)
                            {
                                ValueSet Value = new ValueSet
                                {
                                    {"Success", JsonSerializer.Serialize(Pack)}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }
                            else
                            {
                                ValueSet Value = new ValueSet
                                {
                                    {"Error",  "Could not found the package with PFN"}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }
                            break;
                        }
                    case "Execute_GetAllInstalledApplication":
                        {
                            ValueSet Value = new ValueSet
                            {
                                {"Success", JsonSerializer.Serialize(await Helper.GetInstalledApplicationAsync())}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_CheckPackageFamilyNameExist":
                        {
                            string PFN = Convert.ToString(args.Request.Message["PackageFamilyName"]);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", Helper.CheckIfPackageFamilyNameExist(PFN) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_LaunchUWPLnkFile":
                        {
                            string PFN = Convert.ToString(args.Request.Message["PackageFamilyName"]);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", await Helper.LaunchApplicationFromPackageFamilyName(PFN) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_UpdateLink":
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            string Argument = string.Join(" ", Package.Argument.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para).ToArray());

                            ValueSet Value = new ValueSet();

                            if (File.Exists(Package.LinkPath))
                            {
                                if (Path.IsPathRooted(Package.LinkTargetPath))
                                {
                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.TargetPath = Package.LinkTargetPath;
                                        Link.WorkingDirectory = Package.WorkDirectory;
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.RunAsAdministrator = Package.NeedRunAsAdmin;
                                        Link.Description = Package.Comment;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = (((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }
                                else if (Helper.CheckIfPackageFamilyNameExist(Package.LinkTargetPath))
                                {
                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.Description = Package.Comment;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path is not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_SetFileAttribute":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] AttributeGourp = JsonSerializer.Deserialize<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[]>(Convert.ToString(args.Request.Message["Attributes"]));

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                FileInfo File = new FileInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        File.Attributes |= AttributePair.Value;
                                    }
                                    else
                                    {
                                        File.Attributes &= ~AttributePair.Value;
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else if (Directory.Exists(ExecutePath))
                            {
                                DirectoryInfo Dir = new DirectoryInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (FileInfo SubFile in Helper.GetAllSubFiles(Dir))
                                            {
                                                SubFile.Attributes |= AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes |= AttributePair.Value;
                                        }
                                    }
                                    else
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (FileInfo SubFile in Helper.GetAllSubFiles(Dir))
                                            {
                                                SubFile.Attributes &= ~AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes &= ~AttributePair.Value;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetLnkData":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                StringBuilder ProductCode = new StringBuilder(39);
                                StringBuilder ComponentCode = new StringBuilder(39);

                                if (Msi.MsiGetShortcutTarget(ExecutePath, ProductCode, szComponentCode: ComponentCode).Succeeded)
                                {
                                    uint Length = 0;

                                    StringBuilder ActualPathBuilder = new StringBuilder();

                                    Msi.INSTALLSTATE State = Msi.MsiGetComponentPath(ProductCode.ToString(), ComponentCode.ToString(), ActualPathBuilder, ref Length);

                                    if (State == Msi.INSTALLSTATE.INSTALLSTATE_LOCAL || State == Msi.INSTALLSTATE.INSTALLSTATE_SOURCE)
                                    {
                                        string ActualPath = ActualPathBuilder.ToString();

                                        foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                        {
                                            ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                        }

                                        using (ShellItem Item = new ShellItem(ActualPath))
                                        using (Image IconImage = Item.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk))
                                        using (MemoryStream IconStream = new MemoryStream())
                                        {
                                            Bitmap TempBitmap = new Bitmap(IconImage);
                                            TempBitmap.MakeTransparent();
                                            TempBitmap.Save(IconStream, ImageFormat.Png);

                                            Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, ActualPath, string.Empty, WindowState.Normal, 0, string.Empty, false, IconStream.ToArray())));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Lnk file could not be analysis by MsiGetShortcutTarget");
                                    }
                                }
                                else
                                {
                                    using (ShellLink Link = new ShellLink(ExecutePath))
                                    {
                                        if (string.IsNullOrEmpty(Link.TargetPath))
                                        {
                                            string PackageFamilyName = Helper.GetPackageFamilyNameFromUWPShellLink(ExecutePath);

                                            if (string.IsNullOrEmpty(PackageFamilyName))
                                            {
                                                Value.Add("Error", "TargetPath is invalid");
                                            }
                                            else
                                            {
                                                byte[] IconData = await Helper.GetIconDataFromPackageFamilyName(PackageFamilyName).ConfigureAwait(true);

                                                Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, PackageFamilyName, string.Empty, (WindowState)Enum.Parse(typeof(WindowState), Enum.GetName(typeof(FormWindowState), Link.ShowState)), (int)Link.HotKey, Link.Description, false, IconData)));
                                            }
                                        }
                                        else
                                        {
                                            MatchCollection Collection = Regex.Matches(Link.Arguments, "[^ \"]+|\"[^\"]*\"");

                                            List<string> Arguments = new List<string>(Collection.Count);

                                            foreach (Match Mat in Collection)
                                            {
                                                Arguments.Add(Mat.Value);
                                            }

                                            string ActualPath = Link.TargetPath;

                                            foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                            {
                                                ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                            }

                                            using (Image IconImage = Link.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit | ShellItemGetImageOptions.ScaleUp))
                                            using (MemoryStream IconStream = new MemoryStream())
                                            using (Bitmap TempBitmap = new Bitmap(IconImage))
                                            {
                                                TempBitmap.MakeTransparent();
                                                TempBitmap.Save(IconStream, ImageFormat.Png);

                                                Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, ActualPath, Link.WorkingDirectory, (WindowState)Enum.Parse(typeof(WindowState), Enum.GetName(typeof(FormWindowState), Link.ShowState)), (int)Link.HotKey, Link.Description, Link.RunAsAdministrator, IconStream.ToArray(), Arguments.ToArray())));
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File is not exist");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Intercept_Win_E":
                        {
                            ValueSet Value = new ValueSet();

                            string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                            if (EnvironmentVariables.Where((Var) => Var.Contains("WindowsApps")).Select((Var) => Path.Combine(Var, "RX-Explorer.exe")).FirstOrDefault((Path) => File.Exists(Path)) is string AliasLocation)
                            {
                                StorageFile InterceptFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Intercept_WIN_E.reg"));
                                StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("Intercept_WIN_E_Temp.reg", CreationCollisionOption.ReplaceExisting);

                                using (Stream FileStream = await InterceptFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                using (StreamReader Reader = new StreamReader(FileStream))
                                {
                                    string Content = await Reader.ReadToEndAsync().ConfigureAwait(true);

                                    using (Stream TempStream = await TempFile.OpenStreamForWriteAsync())
                                    using (StreamWriter Writer = new StreamWriter(TempStream, Encoding.Unicode))
                                    {
                                        await Writer.WriteAsync(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                    }
                                }

                                using (Process Process = Process.Start(TempFile.Path))
                                {
                                    SetWindowsZPosition(Process);
                                    Process.WaitForExit();
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Alias file is not exists");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Restore_Win_E":
                        {
                            StorageFile RestoreFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_WIN_E.reg"));

                            using (Process Process = Process.Start(RestoreFile.Path))
                            {
                                SetWindowsZPosition(Process);
                                Process.WaitForExit();
                            }

                            ValueSet Value = new ValueSet
                            {
                                { "Success", string.Empty }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RequestCreateNewPipe":
                        {
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);

                            if (!PipeServers.ContainsKey(Guid))
                            {
                                NamedPipeServerStream NewPipeServer = new NamedPipeServerStream($@"Explorer_And_FullTrustProcess_NamedPipe-{Guid}", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 2048, 2048, null, HandleInheritability.None, PipeAccessRights.ChangePermissions);

                                PipeSecurity Security = NewPipeServer.GetAccessControl();
                                PipeAccessRule ClientRule = new PipeAccessRule(new SecurityIdentifier("S-1-15-2-1"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
                                PipeAccessRule OwnerRule = new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow);
                                Security.AddAccessRule(ClientRule);
                                Security.AddAccessRule(OwnerRule);
                                NewPipeServer.SetAccessControl(Security);

                                PipeServers.Add(Guid, NewPipeServer);

                                _ = NewPipeServer.WaitForConnectionAsync(new CancellationTokenSource(3000).Token).ContinueWith((task) =>
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipe))
                                    {
                                        Pipe.Dispose();
                                        PipeServers.Remove(Guid);
                                    }
                                }, TaskContinuationOptions.OnlyOnCanceled);
                            }

                            break;
                        }
                    case "Identity":
                        {
                            ValueSet Value = new ValueSet
                            {
                                { "Identity", "FullTrustProcess" }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Quicklook":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                QuicklookConnector.SendMessage(ExecutePath);
                            }

                            break;
                        }
                    case "Execute_Check_QuicklookIsAvaliable":
                        {
                            bool IsSuccess = QuicklookConnector.CheckQuicklookIsAvaliable();

                            ValueSet Result = new ValueSet
                            {
                                {"Check_QuicklookIsAvaliable_Result", IsSuccess }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_Associate":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Associate_Result", JsonSerializer.Serialize(ExtensionAssociate.GetAllAssociation(Path)) }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_RecycleBinItems":
                        {
                            ValueSet Result = new ValueSet();

                            string RecycleItemResult = RecycleBinController.GenerateRecycleItemsByJson();

                            if (string.IsNullOrEmpty(RecycleItemResult))
                            {
                                Result.Add("Error", "Could not get recycle items");
                            }
                            else
                            {
                                Result.Add("RecycleBinItems_Json_Result", RecycleItemResult);
                            }

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Empty_RecycleBin":
                        {
                            ValueSet Result = new ValueSet
                            {
                                { "RecycleBinItems_Clear_Result", RecycleBinController.EmptyRecycleBin() }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Restore_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Restore_Result", RecycleBinController.Restore(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_Delete_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Delete_Result", RecycleBinController.Delete(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_EjectUSB":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (string.IsNullOrEmpty(Path))
                            {
                                Value.Add("EjectResult", false);
                            }
                            else
                            {
                                Value.Add("EjectResult", USBController.EjectDevice(Path));
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Execute_Unlock_Occupy":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (File.Exists(Path))
                            {
                                if (StorageItemController.CheckOccupied(Path))
                                {
                                    if (StorageItemController.TryUnoccupied(Path))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "Unoccupied failed");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotOccupy", "The file is not occupied");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFoundOrNotFile", "Path is not a file");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Copy":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (StorageItemController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                {
                                    if (StorageItemController.Copy(SourcePathList, DestinationPath, (s, e) =>
                                    {
                                        lock (Locker)
                                        {
                                            try
                                            {
                                                Progress = e.ProgressPercentage;

                                                if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                {
                                                    using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                    {
                                                        Writer.WriteLine(e.ProgressPercentage);
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                Debug.WriteLine("Could not send progress data");
                                            }
                                        }
                                    },
                                    (se, arg) =>
                                    {
                                        if (arg.Result == HRESULT.S_OK && !IsUndo)
                                        {
                                            if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                            }
                                            else
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                            }
                                        }
                                    }))
                                    {
                                        Value.Add("Success", string.Empty);

                                        if (OperationRecordList.Count > 0)
                                        {
                                            Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while copying the folder");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Move":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (SourcePathList.Any((Item) => StorageItemController.CheckOccupied(Item.Key)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                    {
                                        if (StorageItemController.Move(SourcePathList, DestinationPath, (s, e) =>
                                        {
                                            lock (Locker)
                                            {
                                                try
                                                {
                                                    Progress = e.ProgressPercentage;

                                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                    {
                                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                        {
                                                            Writer.WriteLine(e.ProgressPercentage);
                                                        }
                                                    }
                                                }
                                                catch
                                                {
                                                    Debug.WriteLine("Could not send progress data");
                                                }
                                            }
                                        },
                                        (se, arg) =>
                                        {
                                            if (arg.Result == HRESULT.COPYENGINE_S_DONT_PROCESS_CHILDREN && !IsUndo)
                                            {
                                                if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                }
                                                else
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                }
                                            }
                                        }))
                                        {
                                            Value.Add("Success", string.Empty);
                                            if (OperationRecordList.Count > 0)
                                            {
                                                Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "An error occurred while moving the folder");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while moving the folder");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("Could not send progress data");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Execute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExecutePathJson = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<string> ExecutePathList = JsonSerializer.Deserialize<List<string>>(ExecutePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            try
                            {
                                if (ExecutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                {
                                    if (ExecutePathList.Any((Item) => StorageItemController.CheckOccupied(Item)))
                                    {
                                        Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                    }
                                    else
                                    {
                                        if (ExecutePathList.All((Path) => (Directory.Exists(Path) || File.Exists(Path)) && StorageItemController.CheckPermission(FileSystemRights.Modify, System.IO.Path.GetDirectoryName(Path))))
                                        {
                                            if (StorageItemController.Delete(ExecutePathList, PermanentDelete, (s, e) =>
                                            {
                                                lock (Locker)
                                                {
                                                    try
                                                    {
                                                        Progress = e.ProgressPercentage;

                                                        if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                        {
                                                            using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                            {
                                                                Writer.WriteLine(e.ProgressPercentage);
                                                            }
                                                        }
                                                    }
                                                    catch
                                                    {
                                                        Debug.WriteLine("Could not send progress data");
                                                    }
                                                }
                                            },
                                            (se, arg) =>
                                            {
                                                if (!PermanentDelete && !IsUndo)
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                }
                                            }))
                                            {
                                                Value.Add("Success", string.Empty);

                                                if (OperationRecordList.Count > 0)
                                                {
                                                    Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                                }
                                            }
                                            else
                                            {
                                                Value.Add("Error_Failure", "The specified file could not be deleted");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotFound", "ExecutePath is not a file or directory");
                                }
                            }
                            catch
                            {
                                Value.Add("Error_Failure", "The specified file or folder could not be deleted");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RunExe":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string ExecuteParameter = Convert.ToString(args.Request.Message["ExecuteParameter"]);
                            string ExecuteAuthority = Convert.ToString(args.Request.Message["ExecuteAuthority"]);
                            string ExecuteWindowStyle = Convert.ToString(args.Request.Message["ExecuteWindowStyle"]);
                            string ExecuteWorkDirectory = Convert.ToString(args.Request.Message["ExecuteWorkDirectory"]);

                            bool ExecuteCreateNoWindow = Convert.ToBoolean(args.Request.Message["ExecuteCreateNoWindow"]);
                            bool ShouldWaitForExit = Convert.ToBoolean(args.Request.Message["ExecuteShouldWaitForExit"]);

                            ValueSet Value = new ValueSet();

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                if (StorageItemController.CheckPermission(FileSystemRights.ReadAndExecute, ExecutePath))
                                {
                                    try
                                    {
                                        if (string.IsNullOrEmpty(ExecuteParameter))
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExecutePath;
                                                Process.StartInfo.UseShellExecute = true;
                                                Process.StartInfo.WorkingDirectory = ExecuteWorkDirectory;

                                                if (ExecuteCreateNoWindow)
                                                {
                                                    Process.StartInfo.CreateNoWindow = true;
                                                    Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                                }
                                                else
                                                {
                                                    Process.StartInfo.WindowStyle = (ProcessWindowStyle)Enum.Parse(typeof(ProcessWindowStyle), ExecuteWindowStyle);
                                                }

                                                if (ExecuteAuthority == "Administrator")
                                                {
                                                    Process.StartInfo.Verb = "runAs";
                                                }

                                                Process.Start();

                                                SetWindowsZPosition(Process);

                                                if (ShouldWaitForExit)
                                                {
                                                    Process.WaitForExit();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExecutePath;
                                                Process.StartInfo.Arguments = ExecuteParameter;
                                                Process.StartInfo.UseShellExecute = true;
                                                Process.StartInfo.WorkingDirectory = ExecuteWorkDirectory;

                                                if (ExecuteCreateNoWindow)
                                                {
                                                    Process.StartInfo.CreateNoWindow = true;
                                                    Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                                }
                                                else
                                                {
                                                    Process.StartInfo.WindowStyle = (ProcessWindowStyle)Enum.Parse(typeof(ProcessWindowStyle), ExecuteWindowStyle);
                                                }

                                                if (ExecuteAuthority == "Administrator")
                                                {
                                                    Process.StartInfo.Verb = "runAs";
                                                }

                                                Process.Start();

                                                SetWindowsZPosition(Process);

                                                if (ShouldWaitForExit)
                                                {
                                                    Process.WaitForExit();
                                                }
                                            }
                                        }

                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        Value.Add("Error", $"Path: {ExecutePath}, Parameter: {ExecuteParameter}, Authority: {ExecuteAuthority}, ErrorMessage: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "The specified file could not be executed");
                                }
                            }
                            else
                            {
                                Value.Add("Success", string.Empty);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Test_Connection":
                        {
                            try
                            {
                                if (args.Request.Message.TryGetValue("ProcessId", out object ProcessId) && (ExplorerProcess?.Id).GetValueOrDefault() != Convert.ToInt32(ProcessId))
                                {
                                    ExplorerProcess = Process.GetProcessById(Convert.ToInt32(ProcessId));
                                }
                            }
                            catch
                            {
                                Debug.WriteLine("GetProcess from id failed");
                            }

                            await args.Request.SendResponseAsync(new ValueSet { { "Execute_Test_Connection", string.Empty } });

                            break;
                        }
                    case "Paste_Remote_File":
                        {
                            string Path = Convert.ToString(args.Request.Message["Path"]);

                            ValueSet Value = new ValueSet();

                            if (await Helper.CreateSTATask(() =>
                            {
                                try
                                {
                                    RemoteDataObject Rdo = new RemoteDataObject(Clipboard.GetDataObject());

                                    foreach (RemoteDataObject.DataPackage Package in Rdo.GetRemoteData())
                                    {
                                        try
                                        {
                                            if (Package.ItemType == RemoteDataObject.StorageType.File)
                                            {
                                                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                                if (!Directory.Exists(DirectoryPath))
                                                {
                                                    Directory.CreateDirectory(DirectoryPath);
                                                }

                                                string UniqueName = StorageItemController.GenerateUniquePath(System.IO.Path.Combine(Path, Package.Name));

                                                using (FileStream Stream = new FileStream(UniqueName, FileMode.CreateNew))
                                                {
                                                    Package.ContentStream.CopyTo(Stream);
                                                }
                                            }
                                            else
                                            {
                                                string DirectoryPath = System.IO.Path.Combine(Path, Package.Name);

                                                if (!Directory.Exists(DirectoryPath))
                                                {
                                                    Directory.CreateDirectory(DirectoryPath);
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            Package.Dispose();
                                        }
                                    }

                                    return true;
                                }
                                catch
                                {
                                    return false;
                                }
                            }))
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Clipboard is empty or could not get the content");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Exit":
                        {
                            ExitLocker.Set();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ValueSet Value = new ValueSet
                {
                    {"Error", ex.Message}
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                try
                {
                    Deferral.Complete();
                }
                catch
                {
                    Debug.WriteLine($"Exception was threw when complete the deferral");
                }
            }
        }

        private static void SetWindowsZPosition(Process OtherProcess)
        {
            try
            {
                User32.SetWindowPos(OtherProcess.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error: SetWindowsZPosition threw an error, message: {e.Message}");
            }
        }

        private static void AliveCheck(object state)
        {
            if ((ExplorerProcess?.HasExited).GetValueOrDefault())
            {
                ExitLocker.Set();
            }
        }
    }
}
