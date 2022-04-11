README

>>Team Members :
	APOORV KHOSLA | UFID : 3025-1514

>>What is working?
	All requirements needed to successfully compile and complete the project are working. The twitter clone can perform all functions that is registration/ login, tweeting, re-tweeting, searching whilst WebSocket interface has been deployed to complete the model synchronisation remotely. The complete demonstration is in the attached video which shows a basic UI for the Tweeter system with a backend running on Local host itslef on FSharp.

>>Basic Requirements : Finished
	All basic requirements were completed for the given project.

>>Youtube Video link: https://youtu.be/0MAY-YtyFeA
	Video also present in Zip file for submission.

>>How to run the program ?
	(In Terminal =>for Server end)
	dotnet run 
	(In browser =>for Client end)
	open multiple TweeterPage.html for user perspective

>>Project Summary :

JSon serialisation and deserialization are techniques used to encode objects from the Websocket. Serialization translates object data into string and deserialization translates the string into object.

Server : The code utilises Suave framework. This Suave helps to implement and integrate Websocket and HTTP interfaces involved in the development of the Twitter-clone project. Deployment of AKKA's Actor model which handled the live-feed operations for when a user ups and posts a new tweet or places a request for the same. Various data structures used are Lists, Maps and Sets to perform certain insert, delete and search operaions for various functionalities included in the clone. Suave has helped to understand and implement the WebSocket part of the project at a great scale which is a really benificial functionality for most applications exisiting in the world today.

Client : Client side of the Twitter model has been coded in HTML and JS(JavaScript) (& CSS) to perform WebSocket connections with the server end. A basic HTML view of how the model is running is implemented to allow user/ laymen to understand what they're doing at every step and for easier allowance of registration and login with a seperate page for the same.
The project's aim to make a twitter-clone was a success whilst deploying Suave to manage WebSocket connections to allow remote sending of messages between client and server has also been accomplished. This has allowed JSON APIs and changes to code to allow such managements and handling compared to the part (i) of the Project 4.