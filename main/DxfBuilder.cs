using System;
using System.Text;
using LLutile;

public static class DxfBuilder
{
    public static string BuildDxf(StringBuilder entitiesBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("HEADER");
        sb.AppendLine("  9");
        sb.AppendLine("$DWGCODEPAGE");
        sb.AppendLine("  3");
        sb.AppendLine("UTF-8");
        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("  0");
        sb.AppendLine("SECTION");
        sb.AppendLine("  2");
        sb.AppendLine("ENTITIES");
        sb.Append(entitiesBody);
        sb.AppendLine("  0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("  0");
        sb.AppendLine("EOF");
        return sb.ToString();
    }
}