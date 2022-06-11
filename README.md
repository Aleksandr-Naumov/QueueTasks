# QueueTasks
**Фреймворк для получения "задач" по очереди пользователями с применением SSE (API)**


**Возможности:**
1. Классификация задач для определенных пользователей, например, по уровню/грейду.
2. Определять момент, когда нужно уведомить о появлении задачи пользователя.
3. Задача переназначается на другого пользователя в очереди, если тот отказался от нее. (Даже если ссылка была закрыта и метод для отмены не был вызван)
4. Пользователи не могут брать чужие назначенные задачи, будет срабатывать валидация.


**Применение фреймворка**:
1. Написать свою реализацию **IExtensionService**, **ICurrentOperatorProvider**;
2. services.**AddQueueTasks<YourExtensionService, YourCurrentOperatorProvider>()** - регистрация служб;
3. services.AddMvc().**AddQueueTasksApi()** - добавление API;
4. services.**AddQueueTasksSwaggerDocumentation()** - отображение описания методов API в Swagger;
5. Внедрить **ITasksManager**, где будет происходить создание новых задач, и вызвать один из перегруженных метод **Add**;


**Порядок вызовов методов API:**
1. Сначала клиент должен вызвать метод "take-free", чтобы проверить, есть ли свободные задачи.
Если пришел ответ taskId = null, то необходимо вызвать метод для ожидания новых задач "wait-sse",
если же taskId != null, то очередь пуста и эту задачу можно брать в работу(после полученной задачи уже нужно вызвать ВАЩ метод API для взятия в работу задачи);
2. После вызова метода ожидания новых задач "wait-sse" на клиент будут приходить события каждые 25 секунды для поддержания подключения, 
также когда новая задача прийдет (то есть, когда вызовется метод ITaskManager.Add) клиенту прийдет событие типа 'task' и в data будет храниться json вида - 
{"taskId" = "20", "assigned" = true},
если assigned = true, то от такой задачи клиент не может отказаться, тк она уже назначена на него, 
а если assigned = false, то здесь клиент может делать выбор, брать задачу или нет, тк она еще не назначена на него;
3. После того как задача пришла и assigned = false у оператора есть 40 секунд, чтобы решить брать задачу или нет, если он не успел за эти 40 секунд что-то сделать с задачей, 
то эта задача прийдет следующему оператору в очереди (в этом случае по истечению тайминга нужно вызвать метод "{taskId}/reject");
4. Если оператор решил взять задачу в работу, то вызывается метод "{taskId}/assign", чтобы назначить задачу на себя и 
после этого клиент должен вызвать уже ваш метод API для взятия в работу задачи;
5. Если оператор отказался от задачи (незахотел брать в работу или прошел тайминг 40 секунд), должен вызваться метод "{taskId}/reject", 
чтобы эта задача пришла следующему оператору в очереди;
6. Если оператор не успел вызвать один из методов("{taskId}/reject" и "{taskId}/assign") или закрыл вкладку, 
то максимум через 40 секунд(в будущем можно сделать options, чтобы определять свои тайминги) эта задача прийдет следующему оператору в очереди;

**Примеры использования EventSource (JS):**

- const eventSource = new EventSource("/api/v1/QueueTasks/wait-sse"); // Чтобы авторизоваться можно использовать EventSourcePolyfill
        
- eventSource.onmessage = function(event) {
  console.log("Новое сообщение", event.data);
};

- eventSource.addEventListener('connection', event => {
  alert(${event.data} подключилось);
});

- eventSource.addEventListener('task', event => {
  alert(Пришла таска: ${event.data});
});

**Пример события** - 'event: task\ndata: {"taskId":"171","assigned":false}\n\n'

**Проблемы с которыми можно столкнуться:**
- Сервер отдает все события, когда завершается сам запрос => httpContext.Response.Body может быть присвоен новый Stream в каком-то Middleware,
если вы все-таки что-то хотите сделать со Stream (Response.Body),
то лучше сделайте декоратор для изменения/обогащения его, но никак не создавать новый Stream;
- У некоторых пользователей обрывается соединение(запрос) и начинается новый запрос (постоянный перевызов метода "wait-sse") => 
1) не присылается заголовок Transfer-Encoding, 
это может быть из-за настройка nginx конфига, если копировали со stackoverflow информацию, то нужно добавить в location "chunked_transfer_encoding on;";
2) На клиенте используется http 2.0 => прописать у клиента "--disable-http2" в .exe браузера Google Chrome (целевого решения пока не нашли). 
Kubernetes Ingress при ssl соединении определяется протокол и на уровне nginx уже не правится это, проблема инфраструктурнее;
3) Установленный антивирусник мог пожаловаться;
- Не отображаются на фронте события => добавить в webpack.config "compress: false," - "Исправлен баг работы SSE в webpack.config";
- Падает запрос при долгом ожидании (50 минут) => ???;

Список необходимых заголовках, которые должны приходить с ответом:
- Content-Type: text/event-stream; charset=UTF-8
- Transfer-Encoding: chunked
- Cache-Control: no-cache
- Access-Control-Allow-Origin: *

**Ссылки:**
- [Nginx config](https://stackoverflow.com/questions/13672743/eventsource-server-sent-events-through-nginx "location section") - обязательно **chunked_transfer_encoding on;**
- [Expample on PHP using SSE](https://developer.mozilla.org/ru/docs/Web/API/Server-sent_events/Using_server-sent_events "Пример SSE на PHP")
- [JavaScript SSE](https://learn.javascript.ru/server-sent-events "Как использовать SSE в JS")
- [Add nuget package XML documentation to swagger](https://snede.net/add-nuget-package-xml-documentation-to-swagger/ "Подтягивание xml документации из nuget пакета")