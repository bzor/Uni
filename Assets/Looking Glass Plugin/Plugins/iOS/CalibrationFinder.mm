#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

@interface DocumentPickerDelegate : NSObject <UIDocumentPickerDelegate>
@property (nonatomic, copy) void (^onFileSelected)(NSString *fileContents);
@end

@implementation DocumentPickerDelegate

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    if (urls.count > 0) {
        NSURL *fileURL = urls[0];

        // Start security-scoped access
        BOOL accessGranted = [fileURL startAccessingSecurityScopedResource];
        if (!accessGranted) {
            NSLog(@"Failed to gain security-scoped access to file.");
            if (self.onFileSelected) self.onFileSelected(nil);
            return;
        }

        NSError *error = nil;
        NSData *bookmarkData = [fileURL bookmarkDataWithOptions:0
                                 includingResourceValuesForKeys:nil
                                                  relativeToURL:nil
                                                          error:&error];
        if (error) {
            NSLog(@"Error creating bookmark: %@", error.localizedDescription);
            [fileURL stopAccessingSecurityScopedResource];
            if (self.onFileSelected) self.onFileSelected(nil);
            return;
        }

        [[NSUserDefaults standardUserDefaults] setObject:bookmarkData forKey:@"StoredVolumeBookmark"];
        [[NSUserDefaults standardUserDefaults] synchronize];

        // Attempt to read the file contents
        NSString *fileContents = [NSString stringWithContentsOfURL:fileURL encoding:NSUTF8StringEncoding error:&error];

        [fileURL stopAccessingSecurityScopedResource]; // Stop security-scoped access

        if (error) {
            NSLog(@"Error reading file: %@", error.localizedDescription);
            if (self.onFileSelected) self.onFileSelected(nil);
        } else {
            if (self.onFileSelected) self.onFileSelected(fileContents);
        }
    } else {
        if (self.onFileSelected) self.onFileSelected(nil);
    }
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller {
    NSLog(@"Document picker was cancelled.");
    if (self.onFileSelected) self.onFileSelected(nil);
}

+ (NSURL *)retrieveBookmark {
    NSData *bookmarkData = [[NSUserDefaults standardUserDefaults] objectForKey:@"StoredVolumeBookmark"];
    if (!bookmarkData) {
        return nil;
    }

    BOOL isStale = NO;
    NSError *error = nil;
    NSURL *restoredURL = [NSURL URLByResolvingBookmarkData:bookmarkData
                                 options:0
                           relativeToURL:nil
                     bookmarkDataIsStale:&isStale
                                    error:&error];
    if (error || isStale) {
        NSLog(@"Error resolving bookmark: %@", error.localizedDescription);
        return nil;
    }
    return restoredURL;
}

@end

DocumentPickerDelegate *documentPickerDelegate;

extern "C" void _OpenDocumentPicker(void (*callback)(const char *)) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UIAlertController *alert = [UIAlertController alertControllerWithTitle:@"Load Calibration File"
                                                                       message:@"Select the calibration file (visual.json) to continue."
                                                                preferredStyle:UIAlertControllerStyleAlert];

        UIAlertAction *okAction = [UIAlertAction actionWithTitle:@"OK"
                                                           style:UIAlertActionStyleDefault
                                                         handler:^(UIAlertAction *action) {
            documentPickerDelegate = [[DocumentPickerDelegate alloc] init];
            documentPickerDelegate.onFileSelected = ^(NSString *fileContents) {
                if (fileContents) {
                    callback([fileContents UTF8String]);
                } else {
                    callback(NULL);
                }
                documentPickerDelegate = nil;
            };

            UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:@[@"public.json"] inMode:UIDocumentPickerModeOpen];
            picker.delegate = documentPickerDelegate;
            picker.title = @"Select `visual.json` Calibration File";

            UIWindow *activeWindow = nil;
            for (UIWindowScene *scene in [UIApplication sharedApplication].connectedScenes) {
                if ([scene isKindOfClass:[UIWindowScene class]]) {
                    for (UIWindow *window in scene.windows) {
                        if (window.isKeyWindow) {
                            activeWindow = window;
                            break;
                        }
                    }
                }
                if (activeWindow) break;
            }

            UIViewController *rootVC = activeWindow.rootViewController;
            [rootVC presentViewController:picker animated:YES completion:nil];
        }];

        [alert addAction:okAction];

        UIWindow *activeWindow = nil;
        for (UIWindowScene *scene in [UIApplication sharedApplication].connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                for (UIWindow *window in scene.windows) {
                    if (window.isKeyWindow) {
                        activeWindow = window;
                        break;
                    }
                }
            }
            if (activeWindow) break;
        }

        UIViewController *rootVC = activeWindow.rootViewController;
        [rootVC presentViewController:alert animated:YES completion:nil];
    });
}

extern "C" bool _IsVolumeAccessible() {
    NSURL *volumeURL = [DocumentPickerDelegate retrieveBookmark];
    if (volumeURL) {
        BOOL exists = [[NSFileManager defaultManager] fileExistsAtPath:[volumeURL path]];
        return exists;
    }
    return false;
}

// Define the Unity callback function type (bool parameter)
typedef void (*UnityScreenEventCallback)(bool);

// Store the Unity callback function
static UnityScreenEventCallback screenEventCallback = nullptr;

// Function to register the callback from Unity
extern "C" void _RegisterScreenEventCallback(UnityScreenEventCallback callback) {
    screenEventCallback = callback;
}

// ScreenMonitor class declaration
@interface ScreenMonitor : NSObject
@end

extern "C" bool _InitializeScreenMonitor() {
    static dispatch_once_t onceToken;
    static bool isExternalDisplayConnected = false;

    dispatch_once(&onceToken, ^{
        NSLog(@"[ScreenMonitor] _InitializeScreenMonitor called");

        [[NSNotificationCenter defaultCenter] addObserver:[ScreenMonitor class]
                                                 selector:@selector(screenDidConnect:)
                                                     name:UIScreenDidConnectNotification
                                                   object:nil];

        [[NSNotificationCenter defaultCenter] addObserver:[ScreenMonitor class]
                                                 selector:@selector(screenDidDisconnect:)
                                                     name:UIScreenDidDisconnectNotification
                                                   object:nil];

        // Check if an external display is already connected
        NSArray *screens = [UIScreen screens];
        isExternalDisplayConnected = (screens.count > 1);
        NSLog(@"[ScreenMonitor] Initial external display state: %d", isExternalDisplayConnected);
    });

    return isExternalDisplayConnected;
}

@implementation ScreenMonitor


+ (void)screenDidConnect:(NSNotification *)notification {
    NSLog(@"[ScreenMonitor] Screen Connected");
    if (screenEventCallback) {
        screenEventCallback(true); // Send true for "connected"
    }
}

+ (void)screenDidDisconnect:(NSNotification *)notification {
    NSLog(@"[ScreenMonitor] Screen Disconnected");
    if (screenEventCallback) {
        screenEventCallback(false); // Send false for "disconnected"
    }
}

@end
