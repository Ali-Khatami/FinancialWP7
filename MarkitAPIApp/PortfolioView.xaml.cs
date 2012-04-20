using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.IO.IsolatedStorage;
using System.IO;

namespace MarkitAPIApp
{
    public partial class PortfolioView : PhoneApplicationPage
    {
        private IsolatedStorageFile _WatchlistFile;
        private string _WatchlistFileName;
        private List<string> _WatchList = new List<string>();

        public PortfolioView()
        {
            InitializeComponent();

            this._WatchlistFile = IsolatedStorageFile.GetUserStoreForApplication();
            this._WatchlistFileName = "Watchlist.txt";

            // If the file doesn't exist we need to create it so the user has it for us to reference.
            if (!this._WatchlistFile.FileExists(this._WatchlistFileName))
            {
                this._WatchlistFile.CreateFile(this._WatchlistFileName).Close();
            }

            this._PopulateWatchlist();
        }

        private void _PopulateWatchlist()
        {
            // empty everything from the stackpanel
            this.WatchlistStackPanel.Children.Clear();

            List<string> WatchlistCopy = this._ReadWatchlistFile();

            TextBlock GoToLookupBlock = new TextBlock();
            GoToLookupBlock.Margin = new Thickness(0, 15, 0, 15);
            GoToLookupBlock.Tap += new EventHandler<System.Windows.Input.GestureEventArgs>(this._GoToLookupPage);
            GoToLookupBlock.TextWrapping = TextWrapping.Wrap;

            if (WatchlistCopy.Count == 0)
            {
                GoToLookupBlock.Text = "You don't have any symbols in your watchlist, lame. Tap to find and add some.";
            }
            else
            {
                GoToLookupBlock.Text = "+ Add more symbols to your watchlist.";
            }

            this.WatchlistStackPanel.Children.Add(GoToLookupBlock);

            foreach (string Symbol in WatchlistCopy)
            {
                Button SymbolButton = CreateSymbolButton(Symbol);

                ContextMenuService.SetContextMenu(SymbolButton, CreateSymbolContextMenu(Symbol));

                this.WatchlistStackPanel.Children.Add(SymbolButton);
            }
        }

        private Button CreateSymbolButton(string Symbol)
        {
            Button SymbolButton = new Button();
            SymbolButton.Content = Symbol;
            SymbolButton.Margin = new Thickness(0, 15, 0, 15);            

            return SymbolButton;
        }

        private ContextMenu CreateSymbolContextMenu(string Symbol)
        {
            ContextMenu ContextMenu = new ContextMenu();

            // Add "edit" entry
            MenuItem menuItem = new MenuItem()
            {
                Header = "delete",
                Tag = "delete",
            };

            menuItem.Click += new RoutedEventHandler(this._DeleteSymbolClicked);
            menuItem.Tag = Symbol;
            ContextMenu.Items.Add(menuItem);

            return ContextMenu;
        }

        private void _DeleteSymbolClicked(object sender, RoutedEventArgs Event)
        {
            object Tag = ((MenuItem)sender).Tag;

            MessageBox.Show(string.Format("Delete {0} from your watchlist?", Tag.ToString()), "delete", MessageBoxButton.OKCancel);
            this._RemoveFromWatchlist(Tag.ToString());
            this._PopulateWatchlist();
        }

        private void _GoToLookupPage(object Obj, System.Windows.Input.GestureEventArgs E)
        {
            // go to the portfolio page.
            this.NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
        }

        private bool _UpdateWatchlist()
        {
            try
            {
                // Completely wipe the File and open in.
                StreamWriter StreamWriter = new StreamWriter(new IsolatedStorageFileStream(this._WatchlistFileName, FileMode.Truncate, this._WatchlistFile));

                List<string> DistinctWatchlist = this._WatchList;

                // loop through all the symbols currently in the watchlist and write each one to the file.
                foreach (string Symbol in DistinctWatchlist)
                {
                    StreamWriter.WriteLine(Symbol); //Wrting to the file
                }

                // close the stream writer.
                StreamWriter.Close();

                // everything went according to plan so return true.
                return true;
            }
            catch
            {
                // something went wrong return false.
                return false;
            }
        }

        private bool _ClearWatchlist()
        {
            try
            {
                // Completely wipe the File and open in.
                StreamWriter StreamWriter = new StreamWriter(new IsolatedStorageFileStream(this._WatchlistFileName, FileMode.Truncate, this._WatchlistFile));

                this._WatchList = new List<string>();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool _AddToWatchlist(params string[] Symbols)
        {
            try
            {
                this._WatchList.AddRange(Symbols);
                return this._UpdateWatchlist();
            }
            catch
            {
                // something went wrong, return false.
                return false;
            }
        }

        private bool _RemoveFromWatchlist(params string[] Symbols)
        {
            try
            {
                List<string> WatchlistCopy = this._WatchList;

                foreach (string Symbol in Symbols)
                {
                    if (WatchlistCopy.Contains(Symbol))
                    {
                        WatchlistCopy.Remove(Symbol);
                    }
                }

                this._WatchList = WatchlistCopy;

                return this._UpdateWatchlist();
            }
            catch
            {
                // something went wrong, return false.
                return false;
            }
        }

        private List<string> _ReadWatchlistFile()
        {
            // Clear the watchlist everytime.
            this._WatchList = new List<string>();

            // Create a stream
            StreamReader reader = new StreamReader(new IsolatedStorageFileStream(this._WatchlistFileName, FileMode.Open, this._WatchlistFile));

            // read the data all the way through
            string sRawData = reader.ReadToEnd();

            // close the stream
            reader.Close();

            // split the data into an array that we can iterate over
            string[] arData = sRawData.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            // loop through and populate the watchlist.
            foreach (string Symbol in arData)
            {
                this._WatchList.Add(Symbol);
            }

            return this._WatchList;
        }
    }
}