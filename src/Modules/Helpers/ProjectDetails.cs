namespace LWIR_app;

public static class ProjectDetails
{
    public static string ProjectName = "";
    public static string BaseSensor = "LWIR";
    public static void Import(string[] args)
    {
        if (args.Length < 1) return;

        switch (args[0])
        {
            case "Project":
                ProjectName = args[1];
                BaseSensor = args[2];
            break;
        }
    }
}