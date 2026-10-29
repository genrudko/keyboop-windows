using Keyboop.Core;

var cases = new (string input,string expected)[]{
    ("ghbdtn","привет"),
    ("Привет","Ghbdtn"),
    ("руддщ","hello"),
    ("Hello","Руддщ"),
    ("12345","12345"),
    ("helloПривет","helloПривет"),
    ("",""),
    ("rfr","как")
};
foreach(var (input,expected) in cases){
    var actual=LayoutConverter.Convert(input);
    if(actual!=expected) throw new Exception($"Conversion mismatch: {input}: expected {expected}, got {actual}");
}
Console.WriteLine($"PASS {cases.Length} core smoke cases");