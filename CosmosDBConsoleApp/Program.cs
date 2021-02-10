using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace CosmosDBConsoleApp
{
    class Program
    {
        private const int numConfItems = 4;
        private const string configFileName = "CosmosDBConsoleApp.conf";
        private const string connectionStringItem = "Connection string";
        private const string databaseNameItem = "Database name";
        private const string collectionNameItem = "Collection name";
        private const char configItemDelim = '\t';
        private static FileInfo confFi;
        private static List<MembershipRegistryItem> collectionItems;
        private static string connectionString;
        private static string databaseName;
        private static string collectionName;
        private static MongoClient client;
        private static MongoClientSettings settings;
        private static IMongoDatabase db;
        private static IMongoCollection<MembershipRegistryItem> membershipRegistry;
        private static Queue<ConsoleKeyInfo> keyInfoQueue = new Queue<ConsoleKeyInfo>();
        private static bool errorMessage = false;
        static void Main(string[] args)
        {
            confFi = new FileInfo(configFileName);
            
            if (confFi.Exists)
            {
                try
                {
                    StreamReader confReader = new StreamReader(confFi.FullName);

                    for (int i = 0; i < numConfItems; i++)
                    {
                        string confFileLine = confReader.ReadLine();

                        if (confFileLine == null)
                        {
                            break;
                        }

                        string[] confItem = confFileLine.Split(configItemDelim);

                        if (confItem.Length != 2 || string.IsNullOrEmpty(confItem[1]))
                        {
                            throw new FormatException("virheellinen konfigurointitiedoston muoto.");
                        }
                        else
                        {
                            switch (confItem[0])
                            {
                                case connectionStringItem: connectionString = confItem[1]; break;
                                case databaseNameItem: databaseName = confItem[1]; break;
                                case collectionNameItem: collectionName = confItem[1]; break;
                                default: throw new FormatException("virheellinen konfigurointitiedoston muoto.");
                            }
                        }
                    }

                    confReader.Dispose();
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"Virhe luettaessa konfigurointitiedostoa: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Odottamaton poikkeus: {ex.Message}");
                    return;
                }
            }
            bool writeConfFile = string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(collectionName) ? true : false;

            if (string.IsNullOrEmpty(connectionString))
            {
                do
                {
                    Console.WriteLine("Anna tietokantayhteyden merkkijono (connection string):");
                    connectionString = Console.ReadLine();
                } while (string.IsNullOrEmpty(connectionString));

                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(databaseName))
            {
                do
                {
                    Console.WriteLine("Anna tietokannan nimi:");
                    databaseName = Console.ReadLine();
                } while (string.IsNullOrEmpty(databaseName));

                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(collectionName))
            {
                do
                {
                    Console.WriteLine("Anna kokoelman (collection) nimi:");
                    collectionName = Console.ReadLine();
                } while (string.IsNullOrEmpty(collectionName));

                Console.WriteLine();
            }

            if (writeConfFile)
            {
                WriteConfigFile();
                Console.Clear();
            }
        
            try
            {
                settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
                settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
                client = new MongoClient(settings);
                db = client.GetDatabase(databaseName);
                membershipRegistry = db.GetCollection<MembershipRegistryItem>(collectionName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Virhe yritettäessä muodostaa yhteyttä tietokantaan: {ex.Message}");
                return;
            }

            try
            {
                int tableHeight = Console.WindowHeight - 3;
                int selectedRow = 0;
                int pageNum = 0;
                bool exitApp = false;
                collectionItems = membershipRegistry.Find<MembershipRegistryItem>(new BsonDocument()).ToList();
                Console.CursorVisible = false;

                PrintHeader();

                Console.SetCursorPosition(0, 1);

                while (!exitApp)
                {
                    bool pageRefresh = false;
                    int topmostItem = pageNum * tableHeight;
                    int afterTopmost = collectionItems.Count - pageNum * tableHeight;
                    int i = topmostItem;

                    int j = 0;
                    for (; i < topmostItem + (afterTopmost < tableHeight ? afterTopmost : tableHeight); i++)
                    {
                        Console.BackgroundColor = j % 2 == 0 ? ConsoleColor.DarkGray : ConsoleColor.Black;
                        Console.ForegroundColor = j % 2 == 0 ? ConsoleColor.Black : ConsoleColor.DarkGray;
                        Console.Write($"{{0,-{Console.WindowWidth / 2}}}", $"{collectionItems[i].FirstName} {collectionItems[i].LastName}");
                        Console.WriteLine($"{{0,-{Console.WindowWidth - Console.WindowWidth / 2}}}", $"{collectionItems[i].Email}");
                        j++;
                    }

                    Console.ResetColor();
                    for (i = i - topmostItem; i < tableHeight; i++)
                    {
                        Console.WriteLine($"{{0,-{Console.WindowWidth}}}", " ");
                        j++;
                    }

                    int rowNumLength = collectionItems.Count.ToString("N0").Length;
                    Console.WriteLine($"Rivi: {{0,{rowNumLength}}} ({{1,{rowNumLength}}}-{{2,{rowNumLength}}}/{{3,{rowNumLength}}})", topmostItem + selectedRow + 1, topmostItem + 1, topmostItem + Math.Min(tableHeight, collectionItems.Count - pageNum * tableHeight), collectionItems.Count);

                    ConsoleKeyInfo userInput;

                    do
                    {
                        Console.SetCursorPosition(0, selectedRow + 1);
                        Console.BackgroundColor = selectedRow % 2 == 0 ? ConsoleColor.Red : ConsoleColor.DarkRed;
                        Console.ForegroundColor = selectedRow % 2 == 0 ? ConsoleColor.DarkRed : ConsoleColor.Red;
                        Console.Write($"{{0,-{Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].FirstName} {collectionItems[topmostItem + selectedRow].LastName}");
                        Console.WriteLine($"{{0,-{Console.WindowWidth - Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].Email}");
                        Console.ResetColor();
                        userInput = Console.ReadKey(true);

                        switch (userInput.Key)
                        {
                            case ConsoleKey.DownArrow:
                                if (selectedRow + pageNum * tableHeight < collectionItems.Count - 1)
                                {
                                    if (selectedRow < tableHeight - 1)
                                    {
                                        Console.SetCursorPosition(0, selectedRow + 1);
                                        Console.BackgroundColor = selectedRow % 2 == 0 ? ConsoleColor.DarkGray : ConsoleColor.Black;
                                        Console.ForegroundColor = selectedRow % 2 == 0 ? ConsoleColor.Black : ConsoleColor.DarkGray;
                                        Console.Write($"{{0,-{Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].FirstName} {collectionItems[topmostItem + selectedRow].LastName}");
                                        Console.WriteLine($"{{0,-{Console.WindowWidth - Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].Email}");
                                        selectedRow++;
                                        Console.ResetColor();
                                        Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                        Console.Write($"Rivi: {{0,{rowNumLength}}}", topmostItem + selectedRow + 1);
                                    }
                                    else
                                    {
                                        pageNum++;
                                        selectedRow = 0;
                                        pageRefresh = true;
                                    }
                                }
                                break;

                            case ConsoleKey.UpArrow:
                                if (selectedRow + pageNum * tableHeight > 0)
                                {
                                    if (selectedRow > 0)
                                    {
                                        Console.SetCursorPosition(0, selectedRow + 1);
                                        Console.BackgroundColor = selectedRow % 2 == 0 ? ConsoleColor.DarkGray : ConsoleColor.Black;
                                        Console.ForegroundColor = selectedRow % 2 == 0 ? ConsoleColor.Black : ConsoleColor.DarkGray;
                                        Console.Write($"{{0,-{Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].FirstName} {collectionItems[topmostItem + selectedRow].LastName}");
                                        Console.WriteLine($"{{0,-{Console.WindowWidth - Console.WindowWidth / 2}}}", $"{collectionItems[topmostItem + selectedRow].Email}");
                                        selectedRow--;
                                        Console.ResetColor();
                                        Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                        Console.Write($"Rivi: {{0,{rowNumLength}}}", topmostItem + selectedRow + 1);
                                    }
                                    else
                                    {
                                        pageNum--;
                                        selectedRow = tableHeight - 1;
                                        pageRefresh = true;
                                    }
                                }
                                break;

                            case ConsoleKey.LeftArrow:
                                if (pageNum > 0)
                                {
                                    pageNum--;
                                    pageRefresh = true;
                                }
                                break;

                            case ConsoleKey.RightArrow:
                                if (collectionItems.Count - pageNum * tableHeight > tableHeight)
                                {
                                    pageNum++;
                                    pageRefresh = true;
                                    selectedRow = Math.Min(selectedRow, collectionItems.Count - pageNum * tableHeight - 1);
                                }
                                break;

                            case ConsoleKey.P:
                                if (membershipRegistry.DeleteOne(Builders<MembershipRegistryItem>.Filter.Eq("_id", collectionItems[topmostItem + selectedRow].Id)).DeletedCount == 1)
                                {
                                    collectionItems.RemoveAt(topmostItem + selectedRow);

                                    if (collectionItems.Count != 0 && selectedRow + topmostItem > collectionItems.Count - 1)
                                    {
                                        if (selectedRow == 0)
                                        {
                                            selectedRow = tableHeight - 1;
                                            pageNum--;
                                        }
                                        else
                                        {
                                            selectedRow--;
                                        }
                                    }

                                    pageRefresh = true;
                                }
                                break;

                            case ConsoleKey.L:
                                Console.Clear();
                                Console.WriteLine("Anna lisättävän rivin tiedot tai paina esc-näppäintä peruuttaaksesi.");
                                MembershipRegistryItem newItem = new MembershipRegistryItem();
                                try
                                {
                                    newItem = EditItem(newItem);
                                    membershipRegistry.InsertOne(newItem);
                                    collectionItems.Add(newItem);
                                }
                                catch (OperationCanceledException ex)
                                {
                                }
                                Console.CursorVisible = false;
                                pageRefresh = true;
                                Console.SetCursorPosition(0, 0);
                                PrintHeader();
                                break;

                            case ConsoleKey.M:
                                Console.Clear();
                                Console.WriteLine("Anna muokattavan rivin tiedot tai paina esc-näppäintä peruuttaaksesi.");
                                MembershipRegistryItem editItem = new MembershipRegistryItem(collectionItems[selectedRow + topmostItem]);
                                try
                                {
                                    editItem = EditItem(editItem);
                                    List<BsonElement> modifiedList = new List<BsonElement>(editItem.ToBsonDocument<MembershipRegistryItem>().Where(e => e.Value != (collectionItems[selectedRow + topmostItem]).ToBsonDocument<MembershipRegistryItem>()[e.Name]));
                                    if (modifiedList.Count > 0)
                                    {

                                        editItem.Id = collectionItems[selectedRow + topmostItem].Id;
                                        collectionItems[selectedRow + topmostItem] = editItem;

                                        List<UpdateDefinition<MembershipRegistryItem>> updateDefinitions = new List<UpdateDefinition<MembershipRegistryItem>>();
                                        foreach (var item in modifiedList)
                                        {
                                            updateDefinitions.Add(Builders<MembershipRegistryItem>.Update.Set(item.Name, item.Value));
                                        }

                                        membershipRegistry.UpdateOne(i => i.Id == editItem.Id, Builders<MembershipRegistryItem>.Update.Combine(updateDefinitions));
                                    }
                                }
                                catch (OperationCanceledException ex)
                                {
                                }

                                Console.CursorVisible = false;
                                pageRefresh = true;
                                Console.SetCursorPosition(0, 0);
                                PrintHeader();
                                break;

                            case ConsoleKey.K:
                                Console.Clear();
                                ViewItem(collectionItems[selectedRow + topmostItem]);
                                Console.SetCursorPosition(0, 0);
                                pageRefresh = true;
                                PrintHeader();
                                break;

                            default:
                                Console.Beep();
                                break;
                        }

                    } while ((userInput.Key == ConsoleKey.DownArrow || userInput.Key == ConsoleKey.UpArrow) && !pageRefresh);

                    if (userInput.Key == ConsoleKey.Escape)
                    {
                        exitApp = true;
                        Console.Clear();
                        Console.CursorVisible = true;
                    }
                    else
                    {
                        Console.SetCursorPosition(0, 1);
                    }
                }
            }
            catch (NullReferenceException)
            {
            }
        }
        private static void ViewItem(MembershipRegistryItem item)
        {
            Console.WriteLine();

            Console.WriteLine("Etunimi:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.FirstName);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Sukunimi");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.LastName);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Osoite:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.HomeAddress);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Postinumero:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.ZIP);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Puhelin:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.PhoneNumber);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Email:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.Email);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Jäsenyyden alkupäivämäärä:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(item.MembershipStart);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Paina mitä tahansa nappia poistuaksesi.");
            Console.ResetColor();
            Console.ReadKey();
        }
        private static MembershipRegistryItem EditItem(MembershipRegistryItem item)
        {
            Console.WriteLine();

            Console.WriteLine("Etunimi:");
            Console.Write(item.FirstName);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Sukunimi");
            Console.Write(item.LastName);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Osoite:");
            Console.Write(item.HomeAddress);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Postinumero:");
            Console.Write(item.ZIP);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Puhelin:");
            Console.Write(item.PhoneNumber);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Email:");
            Console.Write(item.Email);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Jäsenyyden alkupäivämäärä:");
            Console.Write(item.MembershipStart);
            Console.WriteLine();
            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Tallenna muutokset");

            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Näppäin:enter=muokkaa|nuoliylös-/alas=siirry lomakkeella ylös/alas|esc=peruuta");
            Console.ResetColor();

            Console.SetCursorPosition(0, 3);
            int cursorTop = Console.CursorTop;
            bool goBack = false;
            int[] cursorPos = new int[] { item.FirstName?.Length ?? 0, item.LastName?.Length ?? 0, item.HomeAddress?.Length ?? 0, item.ZIP?.Length ?? 0, item.PhoneNumber?.Length ?? 0, item.Email?.Length ?? 0, item.MembershipStart?.ToString().Length ?? 0};
            string[] inputFields = new string[] { item.FirstName ?? "", item.LastName ?? "", item.HomeAddress ?? "", item.ZIP ?? "", item.PhoneNumber ?? "", item.Email ?? "", item.MembershipStart?.ToString() ?? "" };
            string[] defaultValues = (string[])inputFields.Clone();
            IEnumerator<string>[] editFields = new IEnumerator<string>[inputFields.Length];
            for (int i = 0; i < editFields.Length; i++)
            {
                editFields[i] = ReadLine(defaultValues[i]).GetEnumerator();
            }

            int itemNum = 0;
            while (!goBack)
            {
                Console.CursorVisible = false;
                if (itemNum == 7)
                {
                    Console.SetCursorPosition(0, cursorTop + itemNum * 3 - 1);
                    Console.BackgroundColor = ConsoleColor.Gray;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("Tallenna muutokset");
                    ConsoleKey submitForm = Console.ReadKey(true).Key;
                    switch (submitForm)
                    {
                        case ConsoleKey.Escape:
                            throw new OperationCanceledException();

                        case ConsoleKey.Enter:
                            if (string.IsNullOrEmpty(inputFields[0]) || string.IsNullOrEmpty(inputFields[1]))
                            {
                                Console.Beep();
                            }
                            else
                            {
                                goBack = true;
                            }
                            break;

                        case ConsoleKey.UpArrow:
                            Console.CursorLeft = 0;
                            Console.BackgroundColor = ConsoleColor.DarkGray;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write("Tallenna muutokset");
                            itemNum--;
                            break;
                    }
                    continue;
                }
                Console.SetCursorPosition(0, cursorTop + itemNum * 3);
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write($"{{0,-{Console.WindowWidth}}}", inputFields[itemNum]);
                Console.ResetColor();
                Console.CursorLeft = 0;
                ConsoleKey key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.Escape:
                        throw new OperationCanceledException();

                    case ConsoleKey.Enter:
                        Console.CursorLeft = cursorPos[itemNum];
                        Console.CursorVisible = true;
                        bool retry = false;
                        do
                        {
                            try
                            {
                                if (!editFields[itemNum].MoveNext())
                                {
                                    editFields[itemNum].Dispose();
                                    editFields[itemNum] = ReadLine(defaultValues[itemNum]).GetEnumerator();
                                    editFields[itemNum].MoveNext();
                                    
                                }
                                inputFields[itemNum] = editFields[itemNum].Current;

                                switch (itemNum)
                                {
                                    case 0:
                                    case 1:
                                        if (!string.IsNullOrEmpty(inputFields[itemNum]) && inputFields[itemNum].All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                                        {
                                            switch (itemNum)
                                            {
                                                case 0:
                                                    item.FirstName = inputFields[itemNum];
                                                    break;

                                                case 1:
                                                    item.LastName = inputFields[itemNum];
                                                    break;
                                            }

                                            retry = false;
                                        }
                                        else
                                        {
                                            int tempCursorLeft = Console.CursorLeft;
                                            int tempCursorTop = Console.CursorTop;
                                            Console.CursorVisible = false;
                                            Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            if (string.IsNullOrEmpty(inputFields[itemNum]))
                                            {
                                                Console.Write("Etu- ja sukunimi vaaditaan!");
                                            }
                                            else
                                            {
                                                Console.Write("Etu-/sukunimi sisältää virheellisiä merkkejä.");
                                            }
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.CursorLeft = tempCursorLeft;
                                            Console.CursorTop = tempCursorTop;
                                            Console.CursorVisible = true;
                                            Console.Beep();
                                            retry = true;
                                            errorMessage = true;
                                        }
                                        break;

                                    case 2:
                                        item.HomeAddress = !string.IsNullOrEmpty(inputFields[itemNum]) ? inputFields[itemNum] : null;
                                        retry = false;
                                        break;

                                    case 3:
                                        if (string.IsNullOrEmpty(inputFields[itemNum]) || (inputFields[itemNum].Length == 5 && inputFields[itemNum].All(c => char.IsNumber(c))))
                                        {
                                            item.ZIP = !string.IsNullOrEmpty(inputFields[itemNum]) ? inputFields[itemNum] : null;
                                            retry = false;
                                        }
                                        else
                                        {
                                            int tempCursorLeft = Console.CursorLeft;
                                            int tempCursorTop = Console.CursorTop;
                                            Console.CursorVisible = false;
                                            Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("Postinumerossa voi olla ainoastaan numeroita ja pituus on oltava 5 merkkiä.");
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.CursorLeft = tempCursorLeft;
                                            Console.CursorTop = tempCursorTop;
                                            Console.CursorVisible = true;
                                            Console.Beep();
                                            retry = true;
                                            errorMessage = true;
                                        }
                                        break;

                                    case 4:
                                        if (string.IsNullOrEmpty(inputFields[itemNum]) || inputFields[itemNum].All(c => char.IsNumber(c) || c == '+' || c == '(' || c == ')'))
                                        {
                                            item.PhoneNumber = inputFields[itemNum];
                                            retry = false;
                                        }
                                        else
                                        {
                                            int tempCursorLeft = Console.CursorLeft;
                                            int tempCursorTop = Console.CursorTop;
                                            Console.CursorVisible = false;
                                            Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("Puhelinnumerossa voi olla vain numeroita ja merkit +, ( ja ).");
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.CursorLeft = tempCursorLeft;
                                            Console.CursorTop = tempCursorTop;
                                            Console.CursorVisible = true;
                                            Console.Beep();
                                            retry = true;
                                            errorMessage = true;
                                        }
                                        break;

                                    case 5:
                                        Regex emailRegex = new Regex(@"^[a-z0-9_.-]+@[a-z0-9_.-]+\.[a-z]{2,6}$", RegexOptions.IgnoreCase);

                                        if (string.IsNullOrEmpty(inputFields[itemNum]) || emailRegex.IsMatch(inputFields[itemNum]))
                                        {
                                            item.Email = !string.IsNullOrEmpty(inputFields[itemNum]) ? inputFields[itemNum].ToLower() : null;
                                            retry = false;
                                        }
                                        else
                                        {
                                            int tempCursorLeft = Console.CursorLeft;
                                            int tempCursorTop = Console.CursorTop;
                                            Console.CursorVisible = false;
                                            Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("Kirjoita sähköpostiosoite kelvollisessa muodossa (esim. matti.meikalainen@microsoft.com)");
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.CursorLeft = tempCursorLeft;
                                            Console.CursorTop = tempCursorTop;
                                            Console.CursorVisible = true;
                                            Console.Beep();
                                            retry = true;
                                            errorMessage = true;
                                        }
                                        break;

                                    case 6:
                                        DateTime parseResult;
                                        if (DateTime.TryParse(inputFields[itemNum], out parseResult) || string.IsNullOrEmpty(inputFields[6]))
                                        {
                                            item.MembershipStart = !string.IsNullOrEmpty(inputFields[itemNum]) ? parseResult : (Nullable<DateTime>)null;
                                            retry = false;
                                        }
                                        else
                                        {
                                            int tempCursorLeft = Console.CursorLeft;
                                            int tempCursorTop = Console.CursorTop;
                                            Console.CursorVisible = false;
                                            Console.SetCursorPosition(0, Console.WindowHeight - 2);
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"Kirjoita kelvollinen päivämäärä-/kellonaika (esim. muodossa {(new DateTime(3333, 11, 22, 0, 44, 55)).ToString().Replace('3', 'v').Replace('1', 'k').Replace('2', 'p').Replace('0', 'h').Replace('4', 'm').Replace('5', 's')}).");
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.CursorLeft = tempCursorLeft;
                                            Console.CursorTop = tempCursorTop;
                                            Console.CursorVisible = true;
                                            Console.Beep();
                                            retry = true;
                                            errorMessage = true;
                                        }
                                        break;
                                }
                            }
                            catch (OperationCanceledException ex)
                            {
                                editFields[itemNum].Dispose();
                                editFields[itemNum] = ReadLine(defaultValues[itemNum]).GetEnumerator();
                                inputFields[itemNum] = defaultValues[itemNum];
                                switch(itemNum)
                                {
                                    case 0:
                                        item.FirstName = inputFields[itemNum];
                                        break;

                                    case 1:
                                        item.LastName = inputFields[itemNum];
                                        break;

                                    case 2:
                                        item.HomeAddress = inputFields[itemNum];
                                        break;

                                    case 3:
                                        item.ZIP = inputFields[itemNum];
                                        break;

                                    case 4:
                                        item.PhoneNumber = inputFields[itemNum];
                                        break;

                                    case 5:
                                        item.Email = inputFields[itemNum];
                                        break;

                                    case 6:
                                        item.MembershipStart = string.IsNullOrEmpty(inputFields[itemNum]) ? null : (Nullable<DateTime>)DateTime.Parse(inputFields[itemNum]);
                                        break;
                                }
                                retry = false;
                            }
                        } while (retry);
                        break;

                    case ConsoleKey.DownArrow when itemNum < 7:
                    case ConsoleKey.UpArrow when itemNum > 0:
                        Console.CursorLeft = 0;
                        Console.ResetColor();
                        Console.Write($"{{0,-{Console.WindowWidth}}}", inputFields[itemNum]);
                        itemNum += key == ConsoleKey.DownArrow ? 1 : -1;
                        break;
                }
            }

            return item;
        }
        private static IEnumerable<string> ReadLine(string prep)
        {
            List<char> currentLine;
            List<char> prevLine;
            int prevCursor = Console.CursorLeft;
            bool inputFinished = false;
            List<char> addedChars = new List<char>();
            Stack<(string line, int cursor)> undoHistory = new Stack<(string line, int cursor)>();
            Stack<(string line, int cursor)> redoHistory = new Stack<(string line, int cursor)>();

            int tempCursor = Console.CursorLeft;
            Console.CursorVisible = false;
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.CursorLeft = 0;
            if (!string.IsNullOrEmpty(prep))
            {
                Console.Write($"{{0,-{Console.WindowWidth}}}", prep);
                currentLine = prep.ToList();
                prevLine = prep.ToList();
            }
            else
            {
                Console.Write($"{{0,-{Console.WindowWidth}}}", ' ');
                currentLine = new List<char>();
                prevLine = new List<char>();
            }
            Console.CursorLeft = tempCursor;
            Console.CursorVisible = true;

            do
            {
                bool modified = false;
                int numDeleted = 0;

                keyInfoQueue.Enqueue(Console.ReadKey(true));

                while(Console.KeyAvailable)
                {
                    keyInfoQueue.Enqueue(Console.ReadKey(true));
                }

                if (errorMessage)
                {
                    int tempCursorLeft = Console.CursorLeft;
                    int tempCursorTop = Console.CursorTop;
                    Console.CursorVisible = false;
                    Console.SetCursorPosition(0, Console.WindowHeight - 2);
                    Console.Write($"{{0,-{Console.WindowWidth}}}", ' ');
                    Console.CursorLeft = tempCursorLeft;
                    Console.CursorTop = tempCursorTop;
                    Console.CursorVisible = true;
                    errorMessage = false;
                }

                ConsoleKeyInfo keyInfo;
                while (keyInfoQueue.TryDequeue(out keyInfo))
                {
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                        case ConsoleKey.Tab:
                            int tempCursorRet = Console.CursorLeft;
                            yield return new string(currentLine.ToArray());
                            Console.ResetColor();
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.CursorLeft = 0;
                            Console.Write($"{{0,-{Console.WindowWidth}}}", new string(currentLine.ToArray()));
                            Console.CursorLeft = tempCursorRet;
                            Console.CursorVisible = true;
                            break;

                        case ConsoleKey.LeftArrow:
                            if (Console.CursorLeft > 0)
                            {
                                Console.CursorLeft--;
                            }
                            break;

                        case ConsoleKey.RightArrow:
                            if (Console.CursorLeft < currentLine.Count)
                            {
                                Console.CursorLeft++;
                            }
                            break;

                        case ConsoleKey.DownArrow:
                        case ConsoleKey.Z when keyInfo.Modifiers == ConsoleModifiers.Control:
                            if (undoHistory.Count > 0)
                            {
                                redoHistory.Push((new string(currentLine.ToArray()), Console.CursorLeft));
                                (string line, int cursor) currentState = undoHistory.Pop();
                                currentLine = currentState.line.ToList();
                                prevLine = new List<char>(currentLine);
                                Console.CursorVisible = false;
                                Console.CursorLeft = 0;
                                Console.Write($"{{0,-{Console.BufferWidth}}}", new string(currentLine.ToArray()));
                                Console.CursorLeft = currentState.cursor;
                                prevCursor = currentState.cursor;
                                Console.CursorVisible = true;
                                modified = false;
                            }
                            break;

                        case ConsoleKey.UpArrow:
                        case ConsoleKey.Y when keyInfo.Modifiers == ConsoleModifiers.Control:
                            if (redoHistory.Count > 0)
                            {
                                undoHistory.Push((new string(currentLine.ToArray()), Console.CursorLeft));
                                (string line, int cursor) currentState = redoHistory.Pop();
                                currentLine = currentState.line.ToList();
                                prevLine = new List<char>(currentLine);
                                Console.CursorVisible = false;
                                Console.CursorLeft = 0;
                                Console.Write($"{{0,-{Console.BufferWidth}}}", new string(currentLine.ToArray()));
                                Console.CursorLeft = currentState.cursor;
                                prevCursor = currentState.cursor;
                                Console.CursorVisible = true;
                                modified = false;
                            }
                            break;

                        case ConsoleKey.Home:
                            Console.CursorLeft = 0;
                            break;

                        case ConsoleKey.End:
                            Console.CursorLeft = currentLine.Count;
                            break;

                        case ConsoleKey.Escape:
                            throw new OperationCanceledException();

                        case ConsoleKey.Backspace:
                            numDeleted++;
                            ConsoleKeyInfo peekItem2;
                            bool hasNext2 = keyInfoQueue.TryPeek(out peekItem2);
                            if (!hasNext2 || peekItem2.Key != ConsoleKey.Backspace)
                            {
                                int deleteCount = Console.CursorLeft - numDeleted < 0 ? Console.CursorLeft : numDeleted;
                                if (deleteCount == 0)
                                {
                                    Console.Beep();
                                    modified = false;
                                }
                                else
                                {
                                    Console.MoveBufferArea(Console.CursorLeft, Console.CursorTop, currentLine.Count - Console.CursorLeft, 1, Console.CursorLeft - deleteCount, Console.CursorTop, ' ', Console.ForegroundColor, Console.BackgroundColor);
                                    if (deleteCount > currentLine.Count - Console.CursorLeft)
                                    {
                                        for (int i = 0; i < deleteCount - (currentLine.Count - Console.CursorLeft); i++)
                                        {
                                            Console.MoveBufferArea(Console.CursorLeft, Console.CursorTop, 1, 1, Console.CursorLeft - deleteCount + (currentLine.Count - Console.CursorLeft) + i, Console.CursorTop);
                                        }
                                    }
                                    currentLine.RemoveRange(Console.CursorLeft - deleteCount, deleteCount);
                                    Console.CursorLeft -= deleteCount;
                                    modified = true;
                                }

                                numDeleted = 0;
                            }
                            break;

                        default:
                            if (char.IsLetterOrDigit(keyInfo.KeyChar) || char.IsWhiteSpace(keyInfo.KeyChar) || char.IsPunctuation(keyInfo.KeyChar) || char.IsSymbol(keyInfo.KeyChar))
                            {
                                addedChars.Add(keyInfo.KeyChar);
                                //currentLine.Add(keyInfo.KeyChar);

                                ConsoleKeyInfo peekItem;
                                bool hasNext = keyInfoQueue.TryPeek(out peekItem);
                                if (!hasNext || peekItem.Key == ConsoleKey.Enter || peekItem.Key == ConsoleKey.Tab ||
                                    peekItem.Key == ConsoleKey.LeftArrow || peekItem.Key == ConsoleKey.RightArrow ||
                                    peekItem.Key == ConsoleKey.Home || peekItem.Key == ConsoleKey.End ||
                                    peekItem.Key == ConsoleKey.Escape || peekItem.Key == ConsoleKey.Backspace ||
                                    (!char.IsLetterOrDigit(peekItem.KeyChar) && !char.IsWhiteSpace(peekItem.KeyChar)))
                                {
                                    if (currentLine.Count + addedChars.Count > Console.BufferWidth)
                                    {
                                        Console.Beep();
                                        modified = false;
                                    }
                                    else
                                    {
                                        currentLine.InsertRange(Console.CursorLeft, addedChars);

                                        if (Console.CursorLeft != currentLine.Count)
                                        {
                                            Console.MoveBufferArea(Console.CursorLeft, Console.CursorTop, currentLine.Count - Console.CursorLeft, 1, Console.CursorLeft + addedChars.Count, Console.CursorTop);
                                        }

                                        Console.Write(new string(addedChars.ToArray()));
                                        modified = true;
                                    }
                                    addedChars.Clear();
                                }
                            }
                            break;
                    }

                    if (inputFinished)
                    {
                        break;
                    }
                }

                if (modified)
                {
                    List<char> tempCurrentLine = new List<char>(currentLine);
                    undoHistory.Push((new string(prevLine.ToArray()), prevCursor));
                    prevLine = currentLine;
                    prevCursor = Console.CursorLeft;
                    currentLine = tempCurrentLine;
                    redoHistory.Clear();
                    modified = false;
                }

            } while (!inputFinished);

            yield return new string(currentLine.ToArray());
        }
        private static void PrintHeader()
        {
            Console.BackgroundColor = ConsoleColor.Green;
            Console.Write($"{{0,-{Console.WindowWidth / 2}}}", "Nimi");
            Console.WriteLine($"{{0,-{Console.WindowWidth - Console.WindowWidth / 2}}}", "Sähköposti");
            Console.ResetColor();

            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Näppäin:l=lisäys|p=poisto|m=muokkaus|k=lisätietoja|nuoliylös/-alas=rivi ylös/alas|nuolivasen/-oikea=edellinen/seuraava sivu|esc=lopeta");
            Console.ResetColor();
        }
        private static void WriteConfigFile()
        {
            StreamWriter confWriter = new StreamWriter(confFi.FullName, false);

            confWriter.WriteLine($"{connectionStringItem}{configItemDelim}{connectionString}");
            confWriter.WriteLine($"{databaseNameItem}{configItemDelim}{databaseName}");
            confWriter.WriteLine($"{collectionNameItem}{configItemDelim}{collectionName}");

            confWriter.Flush();
            confWriter.Dispose();
        }
    }
}
