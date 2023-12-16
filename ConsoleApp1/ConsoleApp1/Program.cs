using System;
using System.IO;
using System.Management;

class FlashDrive
{
    public static bool CheckForFlashDrive(string service)
    {
        return service == "USBSTOR" ? true : false;
    }

    public static void ParseFlashDrive(string DeviceId)
    {

        var lastIndex = DeviceId.LastIndexOf('\\');
        var lastPart = lastIndex >= 0 ? DeviceId.Substring(lastIndex + 1) : DeviceId;

        var query = "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'";

        using (var searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject disk in searcher.Get())
            {
                var pnpDeviceId = disk["PNPDeviceId"].ToString();
                if (pnpDeviceId.Contains(lastPart))
                {
                    foreach (var property in disk.Properties)
                    {
                        if (property.Value != null)
                        {
                            Console.WriteLine($"\t{property.Name}: {property.Value}");
                        }
                    }

                    var logicalDriveLetter = GetLogicalDriveFromPhysicalDrive(disk["Name"].ToString());
                    if (!String.IsNullOrEmpty(logicalDriveLetter))
                    {
                        var depth = 1;
                        Console.WriteLine($"\tContent with depth {depth}");
                        DisplayDirectoryContents(logicalDriveLetter, depth, 0);
                    }
                }
            }
        }
    }

    static void DisplayDirectoryContents(string path, int maxDepth, int currentDepth)
    {
        if (currentDepth > maxDepth) return;

        if (!Directory.Exists(path))
        {
            return;
        }

        DirectoryInfo dirInfo = new DirectoryInfo(path);

        string indent = new string(' ', currentDepth * 2);

        try
        {
            foreach (DirectoryInfo dir in dirInfo.GetDirectories())
            {
                Console.WriteLine($"\t{indent}[D] {dir.Name}");
                DisplayDirectoryContents(dir.FullName, maxDepth, currentDepth + 1);
            }

            foreach (FileInfo file in dirInfo.GetFiles())
            {
                Console.WriteLine($"\t{indent}[F] {file.Name}");
            }
        }
        catch (Exception ex)
        {
        }
    }

    public static string GetLogicalDriveFromPhysicalDrive(string physicalDrive)
    {
        var query = "SELECT * FROM Win32_DiskDrive WHERE DeviceID = '" + physicalDrive.Replace("\\", "\\\\") + "'";
        using (var diskSearcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject disk in diskSearcher.Get())
            {
                foreach (ManagementObject partition in disk.GetRelated("Win32_DiskPartition"))
                {
                    foreach (ManagementObject logicalDisk in partition.GetRelated("Win32_LogicalDisk"))
                    {
                        return logicalDisk["DeviceID"].ToString();
                    }
                }
            }
        }
        return "";
    }
}
class Device
{
    public static void DisplayDeviceInfo(string deviceId)
    {
        var escapedDeviceId = deviceId.Replace("\\", "\\\\");

        try
        {
            var query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{escapedDeviceId}'";
            var searcher = new ManagementObjectSearcher(query);

            var deviceFound = false;
            foreach (var obj in searcher.Get())
            {
                deviceFound = true;
                foreach (var property in obj.Properties)
                {
                    if (property.Value != null)
                    {
                        Console.WriteLine($"\t{property.Name}: {property.Value}");
                    }
                }

                if (FlashDrive.CheckForFlashDrive(obj["Service"].ToString()))
                {
                    Console.WriteLine($"\tDevice: Flash Drive");
                    FlashDrive.ParseFlashDrive(deviceId);
                }
            }
            if (!deviceFound)
            {
                Console.WriteLine("\tDevice with such DeviceId not Found");
            }
        }
        catch (ManagementException e)
        {
            Console.WriteLine($"\tError occured during WMI request: {e.Message}");
        }
    }
}
class DeviceNotification
{
    public static void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
    {
        var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        Console.WriteLine($"USB device {instance["DeviceId"]} connected:");
        if (instance != null)
        {
            Device.DisplayDeviceInfo(instance["DeviceId"].ToString());
        }
    }
    public static void DeviceRemovedEvent(object sender, EventArrivedEventArgs e)
    {
        ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        Console.WriteLine($"USB device {instance["DeviceId"]} disconnected");
    }
}

class Program
{
    static void Main()
    {
        var insertWatcher = new ManagementEventWatcher();
        var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
        insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceNotification.DeviceInsertedEvent);
        insertWatcher.Query = insertQuery;
        insertWatcher.Start();

        var removeWatcher = new ManagementEventWatcher();
        var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
        removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceNotification.DeviceRemovedEvent);
        removeWatcher.Query = removeQuery;
        removeWatcher.Start();

        Console.WriteLine("Type Enter To Exit.");
        Console.ReadLine();

        insertWatcher.Stop();
        removeWatcher.Stop();
    }
}