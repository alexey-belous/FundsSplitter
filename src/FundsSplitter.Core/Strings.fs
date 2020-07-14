namespace FundsSplitter.Core

module Strings = 
    open System
    open System.Text
    open System.Text.RegularExpressions

    let replaceSpaces text = 
        let options = RegexOptions.None;
        let regex = new Regex("[ ]{2,}", options)
        regex.Replace(text, " ")

    let trim (text: string) = text.Trim()

    let joinStr (separator: string) (values: string list) = 
        String.Join(separator, values)