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

namespace MarkitAPIApp
{
	public partial class MainPage : PhoneApplicationPage
	{
		private WebClient _QuoteWebClient;
		private WebClient _LookupWebClient;
		private WebClient _LookupAsTypeWebClient;
		private string _LookupAPIURL = "http://dev.markitondemand.com/Api/Lookup/xml?input={0}";
		private string _QuoteAPIURL = "http://dev.markitondemand.com/Api/Quote/xml?symbol={0}";
		private string _DefaultInputText = "Enter Symbol...";
		private string _CurrentInputValue = "";
		private string _CurrentSymbol = "";

		// Constructor
		public MainPage()
		{
			InitializeComponent();

			// Init WebClient's and set callbacks
			this._LookupWebClient = new WebClient();
			this._LookupWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.LookupSymbolComplete);

			this._LookupAsTypeWebClient = new WebClient();
			this._LookupAsTypeWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.SymbolAutoCompleteComplete);

			this._QuoteWebClient = new WebClient();
			this._QuoteWebClient.OpenReadCompleted += new OpenReadCompletedEventHandler(this.GetQuoteComplete);
			
			this.SymbolTextBox.Text = _DefaultInputText;

		}

		private void GetQuoteBTN_Click(object sender, RoutedEventArgs e)
		{
			this.LookupSymbol();
			SymbolTextBox.Text = _DefaultInputText;
		}

		#region Symbol Auto Complete

		private void SymbolAutoComplete()
		{
			this.ErrorTextBlock.Text = "";
			this._CurrentInputValue = "";
			this._CurrentSymbol = "";
			this._CurrentInputValue = SymbolTextBox.Text;
			// Call the OpenReadAsyc to make a get request, passing the url with the selected search string.
			this._LookupAsTypeWebClient.OpenReadAsync(new Uri(String.Format(this._LookupAPIURL, this._CurrentInputValue)));
		}

		private void SymbolAutoCompleteComplete(object sender, OpenReadCompletedEventArgs e)
		{
        
		}

		private void SymbolAutoCompleteFail(bool IsRealFailure)
		{
        
		}

		#endregion

		#region Lookup Symbol

		private void LookupSymbol()
		{
			this.CompanyDataPanel.Visibility = Visibility.Collapsed;
			this.ProgressBar.Visibility = Visibility.Visible;
			this.ErrorTextBlock.Text = "";
			this._CurrentInputValue = "";
			this._CurrentSymbol = "";
			this._CurrentInputValue = SymbolTextBox.Text;
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
					Symbol = result.Element("Symbol").Value,
					Name = result.Element("Name").Value,
					Exchange = result.Element("Exchange").Value
				};

				List<MODLookupResult> Lookups = new List<MODLookupResult>();

				foreach (MODLookupResult Lookup in searchResults)
				{
					Lookups.Add(Lookup);
				}

				if (Lookups.Count > 0)
				{
					this.GetQuote(Lookups[0].Symbol);
					SymbolTextBlock.Text = Lookups[0].Symbol;
					CompanyNameTextBlock.Text = Lookups[0].Name;
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
			this.ProgressBar.Visibility = Visibility.Collapsed;
			this.SymbolTextBlock.Text = "";
			this.CompanyNameTextBlock.Text = "";
			this.PriceTextBlock.Text = "";
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
					PriceTextBlock.Text = "$" + String.Format("{0:0,0.00}", CurrResult.LastPrice);
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
					this.ProgressBar.Visibility = Visibility.Collapsed;
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
			this.ProgressBar.Visibility = Visibility.Collapsed;
			this.SymbolTextBlock.Text = "";
			this.CompanyNameTextBlock.Text = "";
			this.PriceTextBlock.Text = "";
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
				this.Focus(); // hide the keyboard
			}
		}

		private void SymbolInput_KeyUp(object sender, KeyEventArgs e)
		{            
			//this._LookupAsTypeWebClient.CancelAsync();
			//this.SymbolAutoComplete();
		}

		#endregion

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
}