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
using System.Xml.Linq;
using Microsoft.Phone.Controls;
using System.Xml.Serialization;
using System.Text;
using System.IO;
using Microsoft.Phone.Tasks;
using System.IO.IsolatedStorage;

namespace MarkitAPIApp
{
	public partial class MainPage : PhoneApplicationPage
	{
		private WebClient _QuoteWebClient;
		private WebClient _LookupWebClient;
		private WebClient _NewsWebClient;
		private string _LookupAPIURL = "http://dev.markitondemand.com/Api/Lookup/xml?input={0}";
		private string _QuoteAPIURL = "http://dev.markitondemand.com/Api/Quote/xml?symbol={0}";
        private string _NewsAPIURL = "http://www.google.com/finance/company_news?q={0}&output=rss";
		private string _DefaultInputText = "Enter Symbol...";
		private string _CurrentInputValue = "";
		private string _CurrentSymbol = "";
        private IsolatedStorageFile _WatchlistFile;
        private string _WatchlistFileName;
        private List<string> _WatchList = new List<string>();
		
        // Constructor
		public MainPage()
		{
            this._WatchlistFile = IsolatedStorageFile.GetUserStoreForApplication();
            this._WatchlistFileName = "Watchlist.txt";

            // If the file doesn't exist we need to create it so the user has it for us to reference.
            if (!this._WatchlistFile.FileExists(this._WatchlistFileName))
            {
                this._WatchlistFile.CreateFile(this._WatchlistFileName).Close();
            }

            this._WatchList = this._ReadWatchlistFile();
            
            InitializeComponent();

			// Init WebClient's and set callbacks
			this._LookupWebClient = new WebClient();
			this._LookupWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.LookupSymbolComplete);

            this._NewsWebClient = new WebClient();
            this._NewsWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.NewsSearchComplete);

			this._QuoteWebClient = new WebClient();
			this._QuoteWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.GetQuoteComplete);
			
			this.SymbolTextBox.Text = _DefaultInputText;

		}

		#region News Search

		private void NewsSearch(string Symbol)
		{
			// Call the OpenReadAsyc to make a get request, passing the url with the selected search string.
            this._NewsWebClient.OpenReadAsync(new Uri(String.Format(this._NewsAPIURL, Symbol)));
            this.NewsLoaderPanel.Visibility = Visibility.Visible;
		}

        private void NewsSearchComplete(object sender, OpenReadCompletedEventArgs e)
		{
            XElement ResultXML;

            if (e.Error != null)
            {
                this.NewsSearchFail(true);
            }
            else
            {
                try
                {
                    ResultXML = XElement.Load(e.Result);

                    List<NewsResult> NewsResults = new List<NewsResult>();

                    if (!ResultXML.HasElements || !ResultXML.Elements().First<XElement>().HasElements)
                    {
                        this.NewsSearchFail(false);
                    }

                    IEnumerable<XElement> Items = ResultXML.Elements().First<XElement>().Elements("item");

                    foreach (XElement ItemElement in Items)
                    {
                        if (ItemElement == null || !ItemElement.HasElements)
                        {
                            continue;
                        }

                        DateTime Date = DateTime.MinValue;

                        DateTime.TryParse(_GetXElementValue(ItemElement.Element("pubDate")), out Date);

                        NewsResult Result = new NewsResult
                        {
                            // Get the Title, Description and Url values.
                            Title = _GetXElementValue(ItemElement.Element("title")),
                            Link = _GetXElementValue(ItemElement.Element("link")),
                            PublishedDate = Date
                        };

                        NewsResults.Add(Result);
                    }

                    if (NewsResults.Count > 0)
                    {
                        foreach (NewsResult Item in NewsResults)
                        {
                            TextBlock TitleTextBlock = new TextBlock();
                            TitleTextBlock.Style = (Style)(this.Resources["PhoneTextTitle2Style"]);
                            TitleTextBlock.Text = Item.Title;
                            TitleTextBlock.Tag = Item.Link;
                            TitleTextBlock.TextWrapping = TextWrapping.Wrap;
                            TitleTextBlock.Margin = new Thickness(0,15,0,15);                            
                            TitleTextBlock.Tap += new EventHandler<System.Windows.Input.GestureEventArgs>(this._OpenLinkInBrowser);
                            
                            TextBlock DateTextBlock = new TextBlock();
                            DateTextBlock.Style = (Style)(this.Resources["PhoneTextTitle3Style"]);
                            DateTextBlock.Text = "Published " + ((Item.PublishedDate != DateTime.MinValue) ? Item.PublishedDate.ToShortDateString() : "--");
                            DateTextBlock.Margin = new Thickness(0);

                            this.CompanyNewsPanel.Children.Add(TitleTextBlock);
                            this.CompanyNewsPanel.Children.Add(DateTextBlock);
                        }

                        this.NewsLoaderPanel.Visibility = Visibility.Collapsed;
                        this.CompanyNewsPanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        this.NewsSearchFail(false);
                    }
                }
                catch
                {
                    this.NewsSearchFail(true);
                }
            }
		}

        private void NewsSearchFail(bool IsRealFailure)
		{
            this.NewsLoaderPanel.Visibility = Visibility.Collapsed;
            this.CompanyNewsPanel.Visibility = Visibility.Visible;
            this.CompanyNewsErrorTextBlock.Text = string.Format("Failed to retrieve news articles for {0}.", this._CurrentSymbol);
		}

		#endregion

		#region Lookup Symbol

		private void LookupSymbol()
		{
            this._ResetUI();
			// Call the OpenReadAsyc to make a get request, passing the url with the selected search string.
			this._LookupWebClient.OpenReadAsync(new Uri(String.Format(this._LookupAPIURL, this._CurrentInputValue)));
		}

		private void LookupSymbolComplete(object sender, OpenReadCompletedEventArgs e)
		{
			XElement ResultXML;
			if (e.Error != null)
			{
				this.LookupSymbolFail(true); 
			}
			else
			{
				try
				{
				ResultXML = XElement.Load(e.Result);
				var searchResults =
				from result in ResultXML.Descendants("LookupResult")
				select new MODLookupResult
				{
					// Get the Title, Description and Url values.
                    Symbol = _GetXElementValue(result.Element("Symbol")),
                    Name = _GetXElementValue(result.Element("Name")),
					Exchange = _GetXElementValue(result.Element("Exchange"))
				};

				List<MODLookupResult> Lookups = new List<MODLookupResult>();

				foreach (MODLookupResult Lookup in searchResults)
				{
					Lookups.Add(Lookup);
				}

				if (Lookups.Count > 0)
				{
					this.GetQuote(Lookups[0].Symbol);
                    this.NewsSearch(Lookups[0].Symbol);
					this.SymbolTextBlock.Text = Lookups[0].Symbol;
                    this.AddCompanyLink.Text = string.Format("Add {0} to your watchlist.", Lookups[0].Symbol);
                    this.AddCompanyLink.Tag = Lookups[0].Symbol;
					this.CompanyNameTextBlock.Text = Lookups[0].Name;
				}
				else
				{
					this.LookupSymbolFail(false);
				}
				}
				catch
				{
				this.LookupSymbolFail(true);
				}
			}
		}

		private void LookupSymbolFail(bool IsRealFailure)
		{
			this.QuoteLoaderPanel.Visibility = Visibility.Collapsed;
            this.NewsLoaderPanel.Visibility = Visibility.Collapsed;
			this.SymbolTextBlock.Text = "";
            this.AddCompanyLink.Text = "";
            this.AddCompanyLink.Tag = null;
			this.CompanyNameTextBlock.Text = "";
			this.PriceValue.Text = "";
			this.ErrorTextBlock.Text = (IsRealFailure) ? "Symbol lookup failed." : string.Format("No matches for {0}. Please check your input and try again.", this._CurrentInputValue);
		}

		#endregion

		#region Get Quote

		private void GetQuote(string Symbol)
		{
			// Call the OpenReadAsyc to make a get request, passing the url with the selected search string.
			this._QuoteWebClient.OpenReadAsync(new Uri(String.Format(this._QuoteAPIURL, Symbol)));
		}

		private void GetQuoteComplete(object sender, OpenReadCompletedEventArgs e)
		{
			XElement ResultXML;
			if (e.Error != null)
			{
				this.GetQuoteFail(true); 
			}
			else
			{
				try
				{
				ResultXML = XElement.Load(e.Result);

				List<MODQuoteResult> QuoteResults = new List<MODQuoteResult>();

				foreach (XElement DataElement in ResultXML.Elements("Data"))
				{
					MODQuoteResult Result = new MODQuoteResult
					{
						// Get the Title, Description and Url values.
						Symbol = DataElement.Element("Symbol").Value,
						Name = DataElement.Element("Name").Value
					};

					double dOut = 0;
					double.TryParse(DataElement.Element("LastPrice").Value, out dOut);
					Result.LastPrice = dOut;

					dOut = 0;
					double.TryParse(DataElement.Element("Change").Value, out dOut);
					Result.Change = dOut;

					dOut = 0;
					double.TryParse(DataElement.Element("ChangePercent").Value, out dOut);
					Result.ChangePercent = dOut;

					dOut = 0;
					double.TryParse(DataElement.Element("MarketCap").Value, out dOut);
					Result.MarketCap = dOut;

					DateTime dtOut = DateTime.MinValue;
					DateTime.TryParse(DataElement.Element("Timestamp").Value, out dtOut);
					if(dtOut > DateTime.MinValue)
					{
						Result.Timestamp = dtOut;
					}

					QuoteResults.Add(Result);
				}

				if (QuoteResults.Count > 0)
				{
					MODQuoteResult CurrResult = QuoteResults[0];
					PriceValue.Text = "$" + String.Format("{0:0,0.00}", CurrResult.LastPrice);
					ChangeValue.Text = "$" + String.Format("{0:0,0.00}", CurrResult.Change);
					ChangePCTValue.Text = String.Format("{0:0,0.00}", CurrResult.ChangePercent) + "%";
					MarketCapValue.Text = String.Format("{0:#,,,}", CurrResult.MarketCap);
					
					if (CurrResult.MarketCap >= 1000000000) //>= 1,000,000,000
					{
						MarketCapValue.Text += "B";
					}
					else if (CurrResult.MarketCap >= 1000000) //>= 1,000,000
					{
						MarketCapValue.Text += "M";
					}
					else if (CurrResult.MarketCap >= 1000) //>= 1,000
					{
						MarketCapValue.Text += "K";
					}

					this.CompanyDataPanel.Visibility = Visibility.Visible;
                    this.QuoteLoaderPanel.Visibility = Visibility.Collapsed;                    
				}
				else
				{
					this.GetQuoteFail(false);
				}
				}
				catch
				{
				this.GetQuoteFail(true);
				}
			}
		}

		private void GetQuoteFail(bool IsRealFailure)
		{
			this.QuoteLoaderPanel.Visibility = Visibility.Collapsed;
			this.SymbolTextBlock.Text = "";
            this.AddCompanyLink.Text = "";
            this.AddCompanyLink.Tag = null;
			this.CompanyNameTextBlock.Text = "";
			this.PriceValue.Text = "";
			this.ErrorTextBlock.Text = string.Format("Failed to retrieve quote for {0}.", this._CurrentSymbol);
		}

		#endregion

		#region Symbol Input Events

		private void SymbolInput_Focus(object sender, RoutedEventArgs e)
		{
			if (SymbolTextBox.Text == _DefaultInputText)
			{
				SymbolTextBox.Text = ""; // clear the default text on focus
			}
		}

		private void SymbolInput_Blur(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SymbolTextBox.Text))
			{
				SymbolTextBox.Text = _DefaultInputText;
			}
		}

		private void SymbolInput_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				this.LookupSymbol();
				SymbolTextBox.Text = _DefaultInputText;                
				this.Focus(); // focus on the application to hide the keyboard
			}
		}

		private void SymbolInput_KeyUp(object sender, KeyEventArgs e)
		{            
			//this._LookupAsTypeWebClient.CancelAsync();
			//this.SymbolAutoComplete();
		}

		#endregion

        #region Helpers

        private string _GetXElementValue(XElement Element)
        {
            if (Element != null)
            {
                return Element.Value;
            }
            else
            {
                return null;
            }
        }

        private void _OpenLinkInBrowser(object Obj, System.Windows.Input.GestureEventArgs E)
        {
            WebBrowserTask wbTask = new WebBrowserTask();
            wbTask.Uri = new Uri(((TextBlock)Obj).Tag.ToString());
            wbTask.Show();
        }

        private void _ResetUI()
        {
            this.CompanyDataPanel.Visibility = Visibility.Collapsed;
            this.CompanyNewsPanel.Visibility = Visibility.Collapsed;
            this.QuoteLoaderPanel.Visibility = Visibility.Visible;
            this.NewsLoaderPanel.Visibility = Visibility.Collapsed;
            this.ErrorTextBlock.Text = "";
            this._CurrentInputValue = "";
            this._CurrentSymbol = "";
            this._CurrentInputValue = SymbolTextBox.Text;
        }

        private bool _UpdateWatchlist()
        {
            try
            {
                // Completely wipe the File and open in.
                StreamWriter StreamWriter = new StreamWriter(new IsolatedStorageFileStream(this._WatchlistFileName, FileMode.Truncate, this._WatchlistFile));

                List<string> DistinctWatchlist = this._WatchList.Distinct().ToList();

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

                return this._UpdateWatchlist();
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
                List<string> WatchlistCopy = this._WatchList.Distinct().ToList();

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

        #endregion

        private void AddCompanyLink_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            object Tag = ((TextBlock)sender).Tag;

            if (Tag != null)
            {
                // add the symbol to the watch list.
                this._AddToWatchlist(Tag.ToString());
                // go to the portfolio page.
                this.NavigationService.Navigate(new Uri("/PortfolioView.xaml", UriKind.Relative));
            }
        }
    }

	public class MODLookupResult
	{
		public string Symbol { get; set; }
		public string Name { get; set; }
		public string Exchange { get; set; }
	}

	public class MODQuoteResult
	{
		public string Status { get; set; }
		public string Name { get; set; }
		public string Symbol { get; set; }
		public double LastPrice { get; set; }
		public double Change { get; set; }
		public double ChangePercent { get; set; }
		public DateTime Timestamp { get; set; }
		public double MarketCap { get; set; }
		public double Volume { get; set; }
		public double ChangeYTD { get; set; }
		public double ChangeYTDPCT { get; set; }
		public double High { get; set; }
		public double Low { get; set; }
		public double Open { get; set; }
	}

    public class NewsResult
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public DateTime PublishedDate { get; set; }
    }
}