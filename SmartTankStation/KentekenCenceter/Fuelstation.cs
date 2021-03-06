﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Net;
using MySql.Web;
using MySql.Data;
using MySql.Data.MySqlClient;
namespace CarCenter
{
    class Fuelstation
    {
        public List<Bankaccount> Bankaccounts { get; private set; }
        public List<Owner> Owners { get; private set; }
        public List<Car> AllCars { get; private set; }
        private CommunicationPCs pc;
        private CommunicationArduino ard1;
        private int newAccountNumber = 0;
        private int newAuthenticationNumber = 10000000;
        public Fuelstation()
        {
            Owners = new List<Owner>();
            Bankaccounts = new List<Bankaccount>();
            AllCars = new List<Car>();
            UpdateFromDatabase();
        }
        public void setPC(CommunicationPCs pc)
        {
            this.pc = pc;
        }
        public void setArduinos(CommunicationArduino arduino1)
        {
            ard1 = arduino1;
        }
        public void sendSerialMsg(int whichArduino, String message)
        {
            ard1.SendMessage(message);
        }
        public TypeOfFuel GetFuelType(string licenseplate)
        {
            foreach (Car caritem in AllCars)
            {
                if (caritem.Licenseplate == licenseplate)
                {
                    return caritem.Fueltype;
                }
            }
            Owner owner = newOwnerDialog();
            TypeOfFuel fuelype = pc.AskNewCarFuelType(licenseplate);
            Car newcar = new Car(licenseplate, fuelype, owner);
            AllCars.Add(newcar);
            List<string> types = new List<string>();
            List<string> values = new List<string>();

            types.Add("licenseplate");
            types.Add("fuelType");
            types.Add("Owner");

            values.Add(licenseplate);
            values.Add(fuelype.ToString());
            values.Add(owner.Name);

            SaveToDatabase("127.0.0.1", "fuelstation", "cars", types, values);

            return newcar.Fueltype;
        }
        public Owner newOwnerDialog()
        {
            accountDialog dlg = new accountDialog();
            Owner newowner;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                newowner = dlg.owner;
                dlg.Dispose();
                return newowner;
            }
            return null;
        }
        public Owner getOwner(string licensePlate)
        {

            foreach (Car car in AllCars)
            {
                if (car.Licenseplate == licensePlate)
                {
                    return car.Owner;
                }
            }
            return null;
        }
        public bool checkPin(string pinCode, Owner owner)
        {
            if (owner.Bankaccount.Pincode == pinCode)
            {
                return true;
            }
            return false;
        }
        public decimal CalculatePrice(string licencePlate, decimal amountOfFuel)
        {
            decimal[] fuelPrices = GetFuelPrice();
            decimal PetrolPrice = fuelPrices[0];
            decimal DieselPrice = fuelPrices[2];
            decimal LPGPRice = fuelPrices[3];
            decimal price = 0;
            TypeOfFuel fuelType = GetFuelType(licencePlate);
            switch (fuelType)
            {
                case TypeOfFuel.Petrol:
                    price = amountOfFuel * PetrolPrice;
                    break;
                case TypeOfFuel.Diesel:
                    price = amountOfFuel * DieselPrice;
                    break;
                case TypeOfFuel.LPG:
                    price = amountOfFuel * LPGPRice;
                    break;
            }
            return price;
        }
        public decimal[] GetFuelPrice()
        {
            string htmlcontent = ParseUrl("http://autotraveler.ru/en/netherlands/trend-price-fuel-netherlands.html#.VnLHuErhCM9");
            decimal[] resultarray;
            if (htmlcontent == "not found")
            {
                //If the site doens't load, this will be returned
                resultarray = new decimal[] { 1.60m, 1.65m, 1.25m, 0.75m };
                return resultarray;
            }
            int htmlindex1 = htmlcontent.IndexOf("diffBenzPrice");
            int htmlindex2 = htmlcontent.IndexOf("boxFuel rekPriceFuel");
            string htmlsubstring = htmlcontent.Substring(htmlindex1, htmlindex2 - htmlindex1);
            string[] htmlsplit = htmlsubstring.Split('<');
            decimal petrolPrice = Convert.ToDecimal(htmlsplit[2].Substring(9, 5).Replace('.', ','));
            decimal petrolPrice98 = Convert.ToDecimal(htmlsplit[9].Substring(9, 5).Replace('.', ','));
            decimal dieselPrice = Convert.ToDecimal(htmlsplit[16].Substring(9, 5).Replace('.', ','));
            decimal lpgPrice = Convert.ToDecimal(htmlsplit[23].Substring(9, 5).Replace('.', ','));
            resultarray = new decimal[] { petrolPrice, petrolPrice98, dieselPrice, lpgPrice };
            return resultarray;
        }
        public string ParseUrl(string url)
        {
            WebClient wc = new WebClient();
            return wc.DownloadString(url);
        }
        public void Pay(string licencePlate, decimal amountOfFuel)
        {
            decimal PayAmount = CalculatePrice(licencePlate, amountOfFuel);
            Owner owner = getOwner(licencePlate);
            string AccountNumber = owner.Bankaccount.AccountNumber;
            if (owner != null)
            {
                string pinCode = getPinCodeFromUser(owner, PayAmount);
                if (checkPin(pinCode, owner))
                {
                    if (owner.Bankaccount.Pay(PayAmount))
                    {


                        string MyConString = "SERVER=127.0.0.1;" +
                "DATABASE=fuelstation;" +
                "UID=Admin;" +
                "PASSWORD=123;";
                        try
                        {
                            using (MySqlConnection openCon = new MySqlConnection(MyConString))
                            {
                                //UPDATE `bankaccounts` SET `balance`=100000 WHERE `accountNr`=125;
                                string sstring = "UPDATE `bankaccounts` SET `balance`=" + owner.Bankaccount.Balance.ToString() + " WHERE `accountNr`=" + owner.Bankaccount.AccountNumber.ToString() + ";";
                                //         Console.WriteLine(sstring);
                                using (MySqlCommand querySaveSstring = new MySqlCommand(sstring))
                                {
                                    querySaveSstring.Connection = openCon;
                                    //querySaveStaff.Parameters.Add("@staffName",MySqlDbType.VarChar,30).Value=name;
                                    openCon.Open();
                                    Console.WriteLine(querySaveSstring.CommandText); ;
                                    querySaveSstring.ExecuteNonQuery();
                                    openCon.Close();
                                    MessageBox.Show(String.Format("Pincode correct\n\nCurrent balance: {0}", owner.Bankaccount.Balance));

                                }
                            }
                        }
                        catch
                        {

                        }

                    }
                    else
                    {
                        MessageBox.Show("Not enough balance. Please go inside and pay with cash or else......\n\n\n balance:" + owner.Bankaccount.Balance + "\nPayAmount: " + PayAmount);
                    }
                }
                else
                {
                    MessageBox.Show("pincode incorrect");
                    Pay(licencePlate, amountOfFuel);
                }
            }
        }
        private string getPinCodeFromUser(Owner owner, decimal amountToPay)
        {
            // Create and display an instance of the dialog box
            BankPinCode dlg = new BankPinCode();
            string pinCode = "";
            // Show the dialog and determine the state of the 
            // DialogResult property for the form.
            dlg.lblBalance.Text = "Current Balance: " + owner.Bankaccount.Balance.ToString() + "\n Price: " + amountToPay.ToString();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                pinCode = dlg.PinCode;
                dlg.Dispose();
            }
            return pinCode;
        }
        public Bankaccount getBankaccount(List<string> listBankAccounts, string ownerAccountNumber)
        {
            foreach (string bankAccountString in listBankAccounts)
            {
                string[] dataBankAccount = bankAccountString.Split(',');
                if (dataBankAccount[0] == ownerAccountNumber)
                {
                    decimal balance;
                    if (decimal.TryParse(dataBankAccount[2], out balance))
                    {
                        return new Bankaccount(dataBankAccount[0], dataBankAccount[1], balance);
                    }
                    else
                    {
                        decimal newBalance = 0;
                        return new Bankaccount(dataBankAccount[0], dataBankAccount[1], newBalance);
                    }
                }
            }
            return null;
        }
        private void UpdateFromDatabase()
        {
            List<string> listCars = new List<string>();
            List<string> listBankAccounts = new List<string>();
            List<string> listOwners = new List<string>();
            GetFromSQLDatabase("127.0.0.1", "fuelstation", "cars", ref listCars);
            GetFromSQLDatabase("127.0.0.1", "fuelstation", "bankAccounts", ref listBankAccounts);
            GetFromSQLDatabase("127.0.0.1", "fuelstation", "owners", ref listOwners);
            foreach (string ownerString in listOwners)
            {
                string[] dataOwner = ownerString.Split(',');
                Bankaccount ownerBankAccount = getBankaccount(listBankAccounts, dataOwner[1]);
                if (ownerBankAccount != null)
                {
                    Owners.Add(new Owner(dataOwner[0], ownerBankAccount));


               /*     List<string> ownertypes = new List<string>();
                    ownertypes.Add("name");
                    ownertypes.Add("bankAccount");

                    List<string> ownervalues = new List<string>();
                    ownervalues.Add(dataOwner[0]);
                    ownervalues.Add(ownerBankAccount.AccountNumber);

                    SaveToDatabase("127.0.0.1", "fuelstation", "owners", ownertypes, ownervalues);*/
                }
                else
                {
                    newAccountNumber++;
                    newAuthenticationNumber++;
                    if (newAccountNumber > 9999)
                    {
                        newAuthenticationNumber = 1;
                    }
                    string newAccountNumberString = newAccountNumber.ToString();
                    string newAuthenticationNumberString = newAuthenticationNumber.ToString();
                    while (newAuthenticationNumberString.Length < 3)
                    {
                        newAuthenticationNumberString = "0" + newAuthenticationNumberString;
                    }
                    Bankaccount bankaccount = new Bankaccount(newAccountNumberString, newAuthenticationNumberString, 0);
                    Owners.Add(new Owner(dataOwner[0], bankaccount));

                    List<string> ownertypes = new List<string>();
                    ownertypes.Add("name");
                    ownertypes.Add("bankAccount");

                    List<string> ownervalues = new List<string>();
                    ownervalues.Add(dataOwner[0]);
                    ownervalues.Add(ownerBankAccount.AccountNumber);

                    SaveToDatabase("127.0.0.1", "fuelstation", "owners", ownertypes, ownervalues);
                }
            }
            foreach (string carString in listCars)
            {
                string[] data = carString.Split(',');
                TypeOfFuel fueltype = TypeOfFuel.Unknown;
                switch (data[1])
                {
                    case "Petrol":
                        fueltype = TypeOfFuel.Petrol;
                        break;
                    case "Diesel":
                        fueltype = TypeOfFuel.Diesel;
                        break;
                    case "LPG":
                        fueltype = TypeOfFuel.LPG;
                        break;
                }
                foreach (Owner owner in Owners)
                {
                    if (owner.Name == data[2])
                    {
                        Car car = new Car(data[0], fueltype, owner);
                        AllCars.Add(car);




                      /*  List<string> cartypes = new List<string>();
                        cartypes.Add("licenseplate");
                        cartypes.Add("fueltype");
                        cartypes.Add("owner");

                        List<string> carvalues = new List<string>();
                        carvalues.Add(car.Licenseplate);
                        carvalues.Add(car.Fueltype.ToString());
                        carvalues.Add(car.Owner.Name);

                        SaveToDatabase("127.0.0.1", "fuelstation", "owners", cartypes, carvalues);*/
                        break;
                    }
                }
            }
        }
        public void GetFromSQLDatabase(string databaseAddress, string databaseName, string tableName, ref List<string> items)
        {
            try
            {
                string MyConString = "SERVER=" + databaseAddress + ";" +
                "DATABASE=" + databaseName + ";" +
                "UID=Admin;" +
                "PASSWORD=123;";
                MySqlConnection connection = new MySqlConnection(MyConString);
                MySqlCommand command = connection.CreateCommand();
                MySqlDataReader Reader;
                command.CommandText = "select * from " + tableName;
                connection.Open();
                Reader = command.ExecuteReader();
                while (Reader.Read())
                {
                    string thisrow = "";
                    int i = 0;
                    do
                    {
                        if (i != 0)
                        {
                            thisrow += ",";
                        }
                        thisrow += Reader.GetValue(i).ToString();
                        i++;
                    }
                    while (i < Reader.FieldCount);
                    items.Add(thisrow);
                }
                connection.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                System.Environment.Exit(0);
            }
        }

        public void SaveToDatabase(string databaseAddress, string databaseName, string tableName, List<string> types, List<string> values/* string licenceplate, string fueltype, string owner*/)
        {
            string MyConString = "SERVER=" + databaseAddress + ";" +
                "DATABASE=" + databaseName + ";" +
                "UID=Admin;" +
                "PASSWORD=123;";
            try
            {
                using (MySqlConnection openCon = new MySqlConnection(MyConString))
                {
                    string saveString = insertIntoSQLStringBuilder(tableName, ref types, ref values);
                    //         string sstring = "INSERT INTO `cars` (`licenseplate`, `Fueltype`, `Owner`) VALUES ('EE-00-AA', 'Petrol', 'user1');";
                    //         Console.WriteLine(sstring);
                    using (MySqlCommand querySaveSstring = new MySqlCommand(saveString))
                    {
                        querySaveSstring.Connection = openCon;
                        //querySaveStaff.Parameters.Add("@staffName",MySqlDbType.VarChar,30).Value=name;
                        openCon.Open();
                        Console.WriteLine(querySaveSstring.CommandText); ;
                        querySaveSstring.ExecuteNonQuery();
                        openCon.Close();
                    }
                }
            }
            catch (Exception e)
            {

                MessageBox.Show(e.Message);
            }
        }

        private string insertIntoSQLStringBuilder(string table, ref List<string> types, ref List<string> values)
        {
            string buildedQuery = string.Format("INSERT INTO `{0}` (", table);
            int amoutOfTypes = 0;
            foreach (string type in types)
            {
                amoutOfTypes++;
                buildedQuery += string.Format("`{0}`", type);
                if (amoutOfTypes != values.Count())
                {
                    buildedQuery += ", ";
                }
                else if (amoutOfTypes == types.Count())
                {
                    buildedQuery += ")";
                }
            }

            buildedQuery += " VALUES (";

            int amoutOfValues = 0;

            foreach (string value in values)
            {
                amoutOfValues++;
                buildedQuery += string.Format("'{0}'", value);
                if (amoutOfValues != values.Count())
                {
                    buildedQuery += ", ";
                }
                else if (amoutOfValues == values.Count())
                {
                    buildedQuery += ")";
                }
            }
            buildedQuery += ";";
            return buildedQuery;
        }
    }
}