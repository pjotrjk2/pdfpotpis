# PDFPotpis

## Stack

Technologies used are dot net latest stable version with compatible library for windows. Windowsis only os that will be supported for now.

## Requirements

Built app should have capabilities to:
- open existing PDF files
- save PDF files
- "save as" PDF files
- digitally sign pdf files

App should have capabilities to view pdf files, in some kind of sandobex mode, where script and js scipts should not be executed; secure mode tldr.

App should have capabilities to digitaly sign PDF document with PK located on "Licna karta" that MUP RS is creating. It should have sign button that should bring popup to choose certificate, and with that certificate add signature to PDF.

PDF should contain certificate in metadata or whatever is used, and have visual representation of signature. This visual representation should contain signatory details like name and surname, and also contain some signature ID or whatever is used i guess.

User should be able to choose where to put physically this signature, with drag and drop capability and live preview of signature.

App is going to have "about" section that is clear that no data is used or stored, that all is completely local. It should be in serbian language.

App should have a one or two step install wizard, where it says its gonna install software on PC, that nothing is used, progress of instalation, and validation of completion. There are no options available to users during installation, except folder of instalation.