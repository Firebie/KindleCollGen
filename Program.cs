using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace KindleCollGen
{
  class Program
  {
    static readonly string bookExtensions = "*.mobi";
    static readonly string collFormat = "{0} * {1}";
    static readonly string deviceDocsPath = "/mnt/us/documents";
    static readonly string collectionLocaleName = "@en-GB";

    static readonly string renames = ", Лоренс Джордж=, Л. Дж.;, Лоис МакМастер=, Л. М.;"
      + ", Дэвид =, Д.;, Филип=, Ф.;, Виктор=, В.;, Эрик=, Э.;, Роберт=, Р.;, Дилэни=, Д.;"
      + ", Клемент=, К.;";

    class Rename
    {
      public string From;
      public string To;
    }

    static List<Rename> Renames = new List<Rename>();

    static int Main(string[] args)
    {
      try
      {
        if (args.Length < 1)
          throw new ApplicationException("You have to provide path to device root folder!");

        string root = Util.RemoveTrailingSlash(args[0]);
        if (!Directory.Exists(root))
          throw new ApplicationException("Directory '{0}' doesn't exists!".Args(root));

        string docs = root + "\\documents";
        if (!Directory.Exists(docs))
          throw new ApplicationException("Directory '{0}' doesn't exists!".Args(docs));

        if (!string.IsNullOrWhiteSpace(renames))
        {
          foreach (string item in renames.Split(new char[] { ';' }))
          {
            string[] parts = item.Split(new char[] { '=' });
            if (parts.Length == 2)
              Renames.Add(new Rename { From = parts[0], To = parts[1] });
          }
        }

        var collections = new Dictionary<string, Collection>();
        
        using (var cryptoProvider = new SHA1CryptoServiceProvider())
          CollectAuthorSeries(docs, deviceDocsPath, string.Empty, cryptoProvider, collections);

        string collFile = root + @"\system\collections.json";

        var lastAccess = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        foreach (Collection coll in collections.Values)
          coll.lastAccess = lastAccess;

        var settings = new JsonSerializerSettings();
        settings.Formatting = Formatting.Indented;
        File.WriteAllText(collFile, JsonConvert.SerializeObject(collections, settings));
      }
      catch (System.Exception ex)
      {
        Console.WriteLine("Error: {0}".Args(ex.Message));
        return -1;
      }

      return 0;
    }

    static string RenameName(string text)
    {
      var sb = new StringBuilder(text);

      foreach (Rename item in Renames)
        sb.Replace(item.From, item.To);

      return sb.ToString();
    }
    static void CollectAuthorSeries(string path, string devicePath, string name, HashAlgorithm ha, Dictionary<string, Collection> collections)
    {
      if (!string.IsNullOrWhiteSpace(name))
      {
        Collection coll = null;
        foreach (string book in Directory.GetFiles(path, bookExtensions, SearchOption.TopDirectoryOnly))
        {
          if (coll == null)
          {
            string fullName = RenameName(name) + collectionLocaleName;
            if (!collections.TryGetValue(fullName, out coll))
            {
              coll = new Collection();
              collections.Add(fullName, coll);
            }
          }

          string deviceFilePath = devicePath + "/" + Path.GetFileName(book);
          byte[] utf8Bytes = Encoding.UTF8.GetBytes(deviceFilePath);
          string hash = "*" + string.Join(string.Empty, ha.ComputeHash(utf8Bytes).Select(i => i.ToString("x2")).ToArray());
          coll.items.Add(hash);
        }
      }

      foreach (string subName in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
      {
        string folder = Path.GetFileName(subName);
        CollectAuthorSeries(subName, devicePath + "/" + folder, string.IsNullOrWhiteSpace(name) ? folder : collFormat.Args(name, folder), ha, collections);
      }
    }
  }

  class Collection
  {
    public List<string> items = new List<string>();
    public long         lastAccess;
  }

  static class Util
  {
    public static string Args(this string format, params object[] args)
    {
      return string.Format(format, args);
    }

    public static string RemoveTrailingSlash(string path)
    {
      if (path == null)
        throw new ArgumentNullException("path");

      return path.TrimEnd(new char[] { '\\', '/' });
    }
  }
}
